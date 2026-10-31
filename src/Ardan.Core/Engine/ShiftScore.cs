using System.Text.Json.Serialization;
using Ardan.Core.Alarms;
using Ardan.Core.Layout;
using Ardan.Core.Orders;

namespace Ardan.Core.Engine;

[JsonConverter(typeof(JsonStringEnumConverter<ScoreKind>))]
public enum ScoreKind { Stored, Dispatched, Order, Fix, Penalty, Goal, Level }

[JsonConverter(typeof(JsonStringEnumConverter<GoalKind>))]
public enum GoalKind { Store, Dispatch, OnTime, Fix, Uptime }

/// <summary>Points given or taken for one thing that happened. Spot names the lane, dock or machine it happened at.</summary>
public sealed record ScoreEvent(long Seq, DateTimeOffset Time, ScoreKind Kind, int Points, string Text, string? Spot);

/// <summary>What the shift earns for its work, and what it loses when nobody responds.</summary>
public static class Rewards
{
    public const int Stored = 10;
    public const int Dispatched = 15;
    public const int OnTime = 50;
    public const int Late = -25;
    public const int Fix = 25;
    public const int FastFix = 25;
    public const int MissedFix = -40;
    public const int Returned = -30;

    public static readonly TimeSpan FastFixWithin = TimeSpan.FromMinutes(1);

    public static int ForOrder(OrderType type, int pallets) =>
        pallets * (type == OrderType.Inbound ? Stored : Dispatched) + OnTime;
}

/// <summary>A goal for the shift. Each time it is reached it pays its reward and comes back one tier harder.</summary>
public sealed class Goal(GoalKind kind, double firstTarget, int firstReward)
{
    public GoalKind Kind { get; } = kind;
    public int Tier { get; private set; } = 1;
    public double Progress { get; private set; }
    public double Target => Math.Round(firstTarget * Math.Pow(1.5, Tier - 1));
    public int Reward => (int)Math.Round(firstReward * Math.Pow(1.25, Tier - 1) / 10) * 10;

    public string Title => Kind switch
    {
        GoalKind.Store => $"Store {Target:0} pallets",
        GoalKind.Dispatch => $"Send {Target:0} pallets to the docks",
        GoalKind.OnTime => $"Finish {Target:0} orders on time",
        GoalKind.Fix => $"Fix {Target:0} stops yourself",
        _ => $"Run {Target / 60:0} minutes without a stop",
    };

    internal bool Add(double amount)
    {
        Progress += amount;
        if (Progress < Target) return false;
        Progress = 0;
        Tier++;
        return true;
    }

    internal void Restart() => Progress = 0;
}

/// <summary>
/// The shift as a game: points for work done, a service rating from on-time orders, levels and goals.
/// It only watches the store; nothing here changes how the store runs.
/// </summary>
public sealed class ShiftScore(DateTimeOffset startedAt)
{
    private const int EventsKept = 40;
    private const int RatedOrders = 20;
    private const int LevelStep = 250;
    private readonly LinkedList<ScoreEvent> _events = new();
    private readonly Queue<bool> _rated = new();
    private long _seq;

    public DateTimeOffset StartedAt { get; } = startedAt;
    public int Points { get; private set; }
    public int Earned { get; private set; }
    public int GoalsDone { get; private set; }
    public int Level => LevelFor(Earned);
    public int LevelFrom => PointsFor(Level);
    public int LevelTo => PointsFor(Level + 1);
    public int OrdersRated => _rated.Count;
    public int OrdersOnTime => _rated.Count(onTime => onTime);

    public IReadOnlyList<Goal> Goals { get; } =
    [
        new(GoalKind.Store, 20, 100),
        new(GoalKind.Dispatch, 20, 100),
        new(GoalKind.OnTime, 5, 150),
        new(GoalKind.Fix, 2, 120),
        new(GoalKind.Uptime, 600, 150),
    ];

    /// <summary>Half stars from 0 to 5: every on-time order in the last twenty counts. No finished orders yet means five stars.</summary>
    public double Stars =>
        _rated.Count == 0 ? 5 : Math.Clamp(Math.Round(((double)OrdersOnTime / _rated.Count - 0.5) * 20) / 2, 0, 5);

    public IEnumerable<ScoreEvent> Latest(int count) => _events.Take(count);

    public static int PointsFor(int level) => LevelStep * (level - 1) * level / 2;

    public static int LevelFor(int earned)
    {
        var level = 1;
        while (PointsFor(level + 1) <= earned) level++;
        return level;
    }

    internal void Stored(DateTimeOffset now, string palletId, LaneId lane)
    {
        Add(now, ScoreKind.Stored, Rewards.Stored, $"{palletId} stored in {lane}", lane.ToString());
        Advance(now, GoalKind.Store, 1);
    }

    internal void Dispatched(DateTimeOffset now, string palletId, int dock)
    {
        Add(now, ScoreKind.Dispatched, Rewards.Dispatched, $"{palletId} sent to dock {dock}", $"Dock {dock}");
        Advance(now, GoalKind.Dispatch, 1);
    }

    internal void Completed(DateTimeOffset now, Order order)
    {
        if (order.DueAt < StartedAt) return;

        var onTime = now <= order.DueAt;
        _rated.Enqueue(onTime);
        if (_rated.Count > RatedOrders) _rated.Dequeue();

        var spot = order.Type == OrderType.Outbound ? $"Dock {order.Dock}" : "Infeed";
        if (!onTime)
        {
            Add(now, ScoreKind.Penalty, Rewards.Late, $"{order.Id} finished late", spot);
            return;
        }
        Add(now, ScoreKind.Order, Rewards.OnTime, $"{order.Id} finished on time", spot);
        Advance(now, GoalKind.OnTime, 1);
    }

    internal void Closed(Alarm alarm)
    {
        if (alarm.Action == AlarmAction.None || alarm.ClearedAt is not { } at) return;

        var reinduct = alarm.Action == AlarmAction.Reinduct;
        var spot = reinduct ? "Infeed" : alarm.Source;
        switch (alarm.ClearedBy)
        {
            case Warehouse.Operator:
                var took = at - alarm.RaisedAt;
                Add(at, ScoreKind.Fix, Rewards.Fix, reinduct
                    ? $"{alarm.Subject} re-inducted after {Duration(took)}"
                    : $"{alarm.Source} reset after {Duration(took)}", spot);
                if (took <= Rewards.FastFixWithin) Add(at, ScoreKind.Fix, Rewards.FastFix, "Fast fix bonus", spot);
                Advance(at, GoalKind.Fix, 1);
                break;
            case Warehouse.RemoteSupport:
                Add(at, ScoreKind.Penalty, Rewards.MissedFix, $"{alarm.Source} waited for remote support", spot);
                break;
            case Warehouse.Supervisor:
                Add(at, ScoreKind.Penalty, Rewards.Returned, $"{alarm.Subject} returned to the supplier", spot);
                break;
        }
    }

    internal void Tick(DateTimeOffset now, double seconds, bool running)
    {
        if (running) Advance(now, GoalKind.Uptime, seconds);
        else Goals.First(goal => goal.Kind == GoalKind.Uptime).Restart();
    }

    private void Advance(DateTimeOffset now, GoalKind kind, double amount)
    {
        var goal = Goals.First(g => g.Kind == kind);
        var (title, reward) = (goal.Title, goal.Reward);
        if (!goal.Add(amount)) return;
        GoalsDone++;
        Add(now, ScoreKind.Goal, reward, $"Goal reached: {title}", null);
    }

    private void Add(DateTimeOffset now, ScoreKind kind, int points, string text, string? spot)
    {
        var level = Level;
        Points += points;
        if (points > 0) Earned += points;
        Record(now, kind, points, text, spot);
        if (Level > level) Record(now, ScoreKind.Level, 0, $"Level {Level} reached", null);
    }

    private void Record(DateTimeOffset now, ScoreKind kind, int points, string text, string? spot)
    {
        _events.AddFirst(new ScoreEvent(++_seq, now, kind, points, text, spot));
        if (_events.Count > EventsKept) _events.RemoveLast();
    }

    private static string Duration(TimeSpan span) =>
        span < TimeSpan.FromMinutes(1) ? $"{Math.Max(1, (int)span.TotalSeconds)} s" : $"{(int)span.TotalMinutes} min";
}
