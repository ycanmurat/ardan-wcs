namespace Ardan.Core.Engine;

/// <summary>Pallets stored and dispatched in one minute.</summary>
public sealed record ThroughputPoint(DateTimeOffset Minute, int In, int Out);

/// <summary>Counts pallets in and out per minute for the last two hours.</summary>
public sealed class ThroughputMeter
{
    public const int Hour = 60;
    private const int Kept = Hour * 2;
    private readonly Dictionary<long, (int In, int Out)> _minutes = [];

    public void CountIn(DateTimeOffset time) => Add(time, 1, 0);

    public void CountOut(DateTimeOffset time) => Add(time, 0, 1);

    /// <summary>The last few minutes, oldest first. The current minute comes last.</summary>
    public IReadOnlyList<ThroughputPoint> Minutes(DateTimeOffset now, int count)
    {
        var current = MinuteOf(now);
        var points = new List<ThroughputPoint>(count);
        for (var minute = current - count + 1; minute <= current; minute++)
        {
            _minutes.TryGetValue(minute, out var counted);
            points.Add(new ThroughputPoint(DateTimeOffset.FromUnixTimeSeconds(minute * 60), counted.In, counted.Out));
        }
        return points;
    }

    public (int In, int Out) TotalLastHour(DateTimeOffset now)
    {
        var points = Minutes(now, Hour);
        return (points.Sum(point => point.In), points.Sum(point => point.Out));
    }

    /// <summary>Fills the past with typical numbers so the charts are not empty after a restart.</summary>
    public void WarmUp(DateTimeOffset now, Random random)
    {
        var current = MinuteOf(now);
        for (var minute = current - Kept; minute < current; minute++)
            _minutes[minute] = (random.Next(1, 4), random.Next(1, 4));
    }

    private void Add(DateTimeOffset time, int palletsIn, int palletsOut)
    {
        var minute = MinuteOf(time);
        _minutes.TryGetValue(minute, out var counted);
        _minutes[minute] = (counted.In + palletsIn, counted.Out + palletsOut);

        foreach (var old in _minutes.Keys.Where(key => key < minute - Kept).ToList())
            _minutes.Remove(old);
    }

    private static long MinuteOf(DateTimeOffset time) => time.ToUnixTimeSeconds() / 60;
}
