using System.Text.Json.Serialization;

namespace Ardan.Core.Engine;

[JsonConverter(typeof(JsonStringEnumConverter<EventKind>))]
public enum EventKind { Stored, Retrieved, Dispatched, Received, Rejected, Order, Alarm, Reset, Info }

/// <summary>Something that happened in the store, in one line.</summary>
public sealed record WarehouseEvent(long Seq, DateTimeOffset Time, EventKind Kind, string Text, string? Ref);

/// <summary>A rolling log of recent events, newest first.</summary>
public sealed class EventLog(int capacity = 300)
{
    private readonly LinkedList<WarehouseEvent> _events = new();
    private long _seq;

    public void Add(DateTimeOffset time, EventKind kind, string text, string? reference = null)
    {
        _events.AddFirst(new WarehouseEvent(++_seq, time, kind, text, reference));
        if (_events.Count > capacity) _events.RemoveLast();
    }

    public IEnumerable<WarehouseEvent> Latest(int count) => _events.Take(count);
}
