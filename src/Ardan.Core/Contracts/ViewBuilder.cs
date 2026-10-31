using Ardan.Core.Alarms;
using Ardan.Core.Engine;
using Ardan.Core.Fleet;
using Ardan.Core.Layout;
using Ardan.Core.Orders;
using Ardan.Core.Stock;

namespace Ardan.Core.Contracts;

/// <summary>Turns the live model into read-only views for the API and the screens.</summary>
public static class ViewBuilder
{
    private const int EventsShown = 14;
    private const int ScoreEventsShown = 8;
    private const int SearchLimit = 12;
    private const int ThroughputMinutes = ThroughputMeter.Hour + 9;

    public static LayoutView Layout(Warehouse w) => new(
        w.Layout.Levels,
        w.Layout.Bays,
        w.Layout.LaneDepth,
        w.Layout.BayPitchMetres,
        w.Layout.LevelHeightMetres,
        w.Layout.PalletPitchMetres,
        w.Lifts.Select(lift => lift.Id).ToList(),
        DemoSite.Company,
        DemoSite.Site);

    public static LiveSnapshot Live(Warehouse w, IReadOnlyList<LaneView> changedLanes, long version)
    {
        var (palletsIn, palletsOut) = w.Throughput.TotalLastHour(w.Now);
        var active = w.Alarms.Active;
        var open = w.Orders.Where(order => order.IsOpen).ToList();

        var kpis = new KpiSet(
            Stored: w.Grid.StoredCount,
            Capacity: w.Layout.Capacity,
            Occupancy: w.Grid.Occupancy,
            InLastHour: palletsIn,
            OutLastHour: palletsOut,
            ReadyMachines: w.Taxis.Count(t => t.InService && t.FaultCode is null) + w.Lifts.Count(l => l.FaultCode is null),
            Machines: w.Taxis.Count + w.Lifts.Count,
            AverageCycleSeconds: Math.Round(w.AverageCycleSeconds),
            OpenOrders: open.Count,
            DueSoon: open.Count(o => o.Type == OrderType.Outbound && o.DueAt > w.Now && o.DueAt - w.Now < TimeSpan.FromMinutes(30)),
            Late: open.Count(o => o.IsLate(w.Now)),
            CriticalAlarms: active.Count(a => a.Severity == Severity.Critical),
            WarningAlarms: active.Count(a => a.Severity == Severity.Warning));

        var orders = new OrderCounts(
            open.Count,
            open.Count(o => o.Type == OrderType.Inbound),
            open.Count(o => o.Type == OrderType.Outbound),
            open.Count(o => o.Status is OrderStatus.Exception or OrderStatus.Short));

        return new LiveSnapshot(
            version,
            w.Now,
            w.Speed,
            Health(w),
            kpis,
            w.Throughput.Minutes(w.Now, ThroughputMinutes),
            w.Taxis.Select(taxi => ToView(w, taxi)).ToList(),
            w.Lifts.Select(ToView).ToList(),
            [ToView(w.Infeed), ToView(w.Outfeed)],
            changedLanes,
            active.OrderByDescending(a => a.Severity).ThenByDescending(a => a.RaisedAt).Select(ToView).ToList(),
            w.Events.Latest(EventsShown).Select(ToView).ToList(),
            orders,
            new ClimateView(w.Climate.TemperatureC, Limits.StoreMinC, Limits.StoreMaxC),
            Shift(w.Score),
            Advisor.Suggest(w));
    }

    public static ShiftView Shift(ShiftScore score) => new(
        score.StartedAt,
        score.Points,
        score.Earned,
        score.Level,
        score.LevelFrom,
        score.LevelTo,
        score.Stars,
        score.OrdersRated,
        score.OrdersOnTime,
        score.GoalsDone,
        score.Goals.Select(goal => new GoalView(goal.Kind, goal.Title, goal.Tier, Math.Round(goal.Progress, 1), goal.Target, goal.Reward)).ToList(),
        score.Latest(ScoreEventsShown).ToList());

    public static IReadOnlyList<LaneView> Lanes(Warehouse w) =>
        w.Grid.Lanes.Select(lane => ToView(w, lane)).ToList();

    public static LaneDetail? LaneDetail(Warehouse w, LaneId id)
    {
        if (!w.Grid.Contains(id)) return null;
        var lane = w.Grid[id];
        return new LaneDetail(ToView(w, lane), lane.Pallets.Reverse().Select(pallet => ToView(w, pallet)).ToList());
    }

    public static IReadOnlyList<OrderView> Orders(Warehouse w, string? view)
    {
        IEnumerable<Order> rows = view switch
        {
            "closed" => w.Orders.Where(o => !o.IsOpen).OrderByDescending(o => o.CompletedAt),
            "all" => w.Orders.OrderByDescending(o => o.CreatedAt),
            _ => w.Orders.Where(o => o.IsOpen).OrderByDescending(o => o.Priority).ThenBy(o => o.DueAt),
        };
        return rows.Select(order => ToView(w, order)).ToList();
    }

    public static IReadOnlyList<SkuView> Skus(Warehouse w) =>
        w.Skus.Values
            .Select(sku =>
            {
                var lanes = w.Grid.Lanes.Where(lane => lane.Front?.SkuCode == sku.Code).ToList();
                DateOnly? nextExpiry = lanes.Count == 0 ? null : lanes.Min(lane => lane.Front!.Expiry);
                return new SkuView(
                    sku.Code,
                    sku.Name,
                    sku.Category,
                    sku.Velocity,
                    lanes.Sum(lane => lane.Count),
                    lanes.Count,
                    w.FreeStock(sku.Code),
                    nextExpiry,
                    sku.CasesPerPallet);
            })
            .OrderBy(sku => sku.Code)
            .ToList();

    public static IReadOnlyList<PalletView> PalletsOf(Warehouse w, string skuCode) =>
        w.Grid.Lanes
            .Where(lane => lane.Front?.SkuCode == skuCode)
            .SelectMany(lane => lane.Pallets.Reverse())
            .OrderBy(pallet => pallet.Expiry)
            .ThenBy(pallet => pallet.Lane?.Level)
            .ThenBy(pallet => pallet.Lane?.Bay)
            .Select(pallet => ToView(w, pallet))
            .ToList();

    public static PalletView? FindPallet(Warehouse w, string id) =>
        w.Pallets.TryGetValue(id, out var pallet) ? ToView(w, pallet) : null;

    public static IReadOnlyList<SearchHit> Search(Warehouse w, string? query)
    {
        var q = query?.Trim() ?? "";
        if (q.Length < 2) return [];

        bool Match(string? text) => text?.Contains(q, StringComparison.OrdinalIgnoreCase) == true;

        var hits = new List<SearchHit>();
        hits.AddRange(w.Taxis.Where(t => Match(t.Id))
            .Select(t => new SearchHit("Taxi", t.Id, t.Id, Activity(t), $"/fleet?select={t.Id}")));
        hits.AddRange(w.Lifts.Where(l => Match(l.Id))
            .Select(l => new SearchHit("VTU", l.Id, l.Id, Activity(l), $"/fleet?select={l.Id}")));
        hits.AddRange(w.Orders.Where(o => Match(o.Id) || Match(o.Reference))
            .Select(o => new SearchHit("Order", o.Id, $"{o.Id} · {o.Reference}", $"{o.Quantity} × {w.NameOf(o.SkuCode)}", $"/orders?select={o.Id}")));
        hits.AddRange(w.Skus.Values.Where(s => Match(s.Code) || Match(s.Name))
            .Select(s => new SearchHit("Product", s.Code, s.Name, $"{s.Code} · {w.Grid.StoredOf(s.Code)} pallets in store", $"/inventory?sku={s.Code}")));
        hits.AddRange(w.Pallets.Values.Where(p => Match(p.Id) || Match(p.Batch)).Take(6)
            .Select(p => new SearchHit("Pallet", p.Id, p.Id, $"{w.NameOf(p.SkuCode)} · {Location(w, p)}", $"/inventory?pallet={p.Id}")));
        return hits.Take(SearchLimit).ToList();
    }

    public static LaneView ToView(Warehouse w, Lane lane)
    {
        var front = lane.Front;
        return new LaneView(
            lane.Id.Level,
            lane.Id.Bay,
            lane.Count,
            lane.Depth,
            front?.SkuCode,
            front is null ? null : w.NameOf(front.SkuCode),
            front?.Batch,
            front?.Expiry,
            lane.IsBusy,
            lane.Version);
    }

    public static PalletView ToView(Warehouse w, Pallet pallet) => new(
        pallet.Id,
        pallet.SkuCode,
        w.NameOf(pallet.SkuCode),
        pallet.Batch,
        pallet.Expiry,
        pallet.WeightKg,
        pallet.HeightMm,
        pallet.Status,
        Location(w, pallet),
        pallet.OrderId,
        pallet.ReceivedAt);

    public static OrderView ToView(Warehouse w, Order order) => new(
        order.Id,
        order.Type,
        order.Reference,
        order.SkuCode,
        w.NameOf(order.SkuCode),
        order.Quantity,
        order.Done,
        order.InFlight,
        order.Rejected,
        order.Dock,
        order.Priority,
        order.Status,
        order.Source,
        order.CreatedAt,
        order.DueAt,
        order.CompletedAt,
        order.Note);

    public static AlarmView ToView(Alarm alarm) => new(
        alarm.Id,
        alarm.Code,
        alarm.Severity,
        alarm.State,
        alarm.Source,
        alarm.Title,
        alarm.What,
        alarm.Cause,
        alarm.Steps,
        alarm.Action,
        alarm.ActionLabel,
        alarm.RaisedAt,
        alarm.AcknowledgedAt,
        alarm.ClearedAt,
        alarm.ClearedBy);

    private static TaxiView ToView(Warehouse w, Taxi taxi) => new(
        taxi.Id,
        taxi.State,
        taxi.Level,
        Math.Round(taxi.X, 3),
        Math.Round(taxi.Battery, 1),
        taxi.Load?.Id,
        taxi.Load is null ? null : w.NameOf(taxi.Load.SkuCode),
        taxi.Job?.Id,
        taxi.Job?.Kind,
        taxi.Job?.From is { } from ? Spot(from) : null,
        taxi.Job?.To is { } to ? Spot(to) : null,
        Handling(taxi),
        taxi.Lift?.Id,
        taxi.SpeedMs,
        Math.Round(taxi.MotorTempC, 1),
        taxi.Signal,
        Math.Round(taxi.DistanceMetres / 1000, 2),
        taxi.InService,
        taxi.ChargeHold,
        taxi.FaultCode,
        Activity(taxi));

    /// <summary>A place as the screens name it: Infeed, Outfeed or a lane such as L2·B05.</summary>
    public static string Spot(Place place) => place.Kind switch
    {
        PlaceKind.Infeed => "Infeed",
        PlaceKind.Outfeed => "Outfeed",
        _ => place.Lane.ToString(),
    };

    /// <summary>How far a Taxi is through picking up or setting down a pallet, from 0 to 1. Null when it is not handling one.</summary>
    private static double? Handling(Taxi taxi) =>
        taxi.State is TaxiState.Loading or TaxiState.Unloading
        && taxi.TransferLeft is { } left
        && taxi.Route.TryPeek(out var leg)
        && leg is Transfer { Seconds: > 0 } transfer
            ? Math.Round(Math.Clamp(1 - left / transfer.Seconds, 0, 1), 3)
            : null;

    private static LiftView ToView(Lift lift)
    {
        var onBoard = lift.State is LiftState.Boarding or LiftState.Carrying or LiftState.Leaving ? lift.Active?.Taxi : null;
        return new LiftView(
            lift.Id,
            lift.Shaft,
            Math.Round(lift.Position, 3),
            lift.State,
            onBoard?.Id,
            onBoard?.Load is not null,
            lift.Waiting,
            lift.FaultCode,
            Activity(lift));
    }

    private static ConveyorView ToView(Conveyor conveyor) => new(
        conveyor.Id,
        conveyor.Name,
        conveyor.State,
        conveyor.Length,
        conveyor.Items.Select(item => new ConveyorItemView(item.Pallet.Id, item.Pallet.SkuCode, Math.Round(item.Position, 3))).ToList(),
        conveyor.FaultCode);

    private static EventView ToView(WarehouseEvent e) => new(e.Seq, e.Time, e.Kind, e.Text, e.Ref);

    private static SystemHealth Health(Warehouse w)
    {
        var stopped = w.Taxis.Count(t => t.FaultCode is not null)
            + w.Lifts.Count(l => l.FaultCode is not null)
            + (w.Outfeed.FaultCode is null ? 0 : 1);
        if (stopped > 0)
            return new SystemHealth(HealthLevel.Attention, stopped == 1 ? "1 machine stopped" : $"{stopped} machines stopped");

        var warnings = w.Alarms.Active.Count;
        return new SystemHealth(HealthLevel.Running, warnings switch
        {
            0 => "All systems running",
            1 => "Running · 1 warning",
            _ => $"Running · {warnings} warnings",
        });
    }

    private static string Activity(Taxi t) => t switch
    {
        { FaultCode: not null } => "Stopped. Needs a reset",
        { State: TaxiState.OutOfService } => "Out of service",
        { State: TaxiState.WaitingForLift } => "Waiting for a VTU",
        { State: TaxiState.OnLift, Lift: { } lift } => $"Riding {lift.Id}",
        { State: TaxiState.WaitingForAisle } => $"Waiting for the level {t.Level} aisle to clear",
        { State: TaxiState.Loading, Job: { } job } => $"Picking up {job.Pallet.Id}",
        { State: TaxiState.Unloading, Job: { Kind: JobKind.Putaway } job } => $"Setting down {job.Pallet.Id} in {job.To}",
        { State: TaxiState.Unloading, Job: { } job } => $"Handing {job.Pallet.Id} to the outfeed",
        { Job: { } job, Load: null } => $"Going to {job.From} for {job.Pallet.Id}",
        { Job: { Kind: JobKind.Putaway } job } => $"Taking {job.Pallet.Id} to {job.To}",
        { Job: { } job } => $"Taking {job.Pallet.Id} to the outfeed",
        { State: TaxiState.Charging, ChargeHold: true } => $"Charging to {Limits.ChargedBattery:0}%",
        { State: TaxiState.Charging } => "Charging at the dock",
        { InService: false } => "Parking at the dock",
        { State: TaxiState.Driving } => "Returning to the dock",
        _ => "Ready for work",
    };

    private static string Activity(Lift l) => l switch
    {
        { FaultCode: not null } => "Stopped. Needs a reset",
        { State: LiftState.Moving, Active: { } ride } => $"Going to level {ride.FromLevel} for {ride.Taxi.Id}",
        { State: LiftState.Boarding, Active: { } ride } => $"{ride.Taxi.Id} boarding",
        { State: LiftState.Carrying, Active: { } ride } => $"Carrying {ride.Taxi.Id} to level {ride.ToLevel}",
        { State: LiftState.Leaving, Active: { } ride } => $"{ride.Taxi.Id} leaving at level {ride.ToLevel}",
        _ => $"Idle at level {Math.Round(l.Position)}",
    };

    private static string Location(Warehouse w, Pallet pallet) => pallet.Status switch
    {
        PalletStatus.Stored when pallet.Lane is { } lane => $"{lane} · place {w.Layout.LaneDepth - pallet.Slot} of {w.Layout.LaneDepth}",
        PalletStatus.Moving when w.Taxis.FirstOrDefault(t => t.Load == pallet) is { } taxi => $"On {taxi.Id}",
        PalletStatus.Moving => "On the outfeed",
        PalletStatus.Arriving => "On the infeed",
        PalletStatus.Rejected => "Reject lane",
        PalletStatus.Dispatched => "Dispatched",
        PalletStatus.Returned => "Returned to the supplier",
        _ => "Unknown",
    };
}

/// <summary>Builds live snapshots and only includes the lanes that changed since the last one.</summary>
public sealed class LiveSnapshots
{
    private readonly Dictionary<LaneId, int> _sent = [];
    private long _version;

    public LiveSnapshot Next(Warehouse w)
    {
        var changed = new List<LaneView>();
        foreach (var lane in w.Grid.Lanes)
        {
            if (_sent.TryGetValue(lane.Id, out var version) && version == lane.Version) continue;
            _sent[lane.Id] = lane.Version;
            changed.Add(ViewBuilder.ToView(w, lane));
        }
        return ViewBuilder.Live(w, changed, ++_version);
    }

    public LiveSnapshot Current(Warehouse w) => ViewBuilder.Live(w, [], _version);
}
