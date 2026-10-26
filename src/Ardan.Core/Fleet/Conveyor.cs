using System.Text.Json.Serialization;
using Ardan.Core.Layout;
using Ardan.Core.Stock;

namespace Ardan.Core.Fleet;

[JsonConverter(typeof(JsonStringEnumConverter<ConveyorState>))]
public enum ConveyorState { Running, Jammed }

/// <summary>A pallet on a conveyor. Position is in metres from the start.</summary>
public sealed class ConveyorItem(Pallet pallet)
{
    public Pallet Pallet { get; } = pallet;
    public double Position { get; internal set; }
    public bool Checked { get; internal set; }
    public string? JobId { get; internal set; }
}

/// <summary>A roller conveyor between the dock and the front of the rack. Pallets queue with a safe gap.</summary>
public sealed class Conveyor(string id, string name)
{
    private readonly List<ConveyorItem> _items = [];

    public string Id { get; } = id;
    public string Name { get; } = name;
    public double Length => Motion.ConveyorLength;
    public ConveyorState State { get; internal set; } = ConveyorState.Running;
    public string? FaultCode { get; internal set; }
    public DateTimeOffset? FaultSince { get; internal set; }
    public IReadOnlyList<ConveyorItem> Items => _items;

    public bool EntryClear =>
        State == ConveyorState.Running && _items.All(item => item.Position >= Motion.ConveyorSpacing);

    public ConveyorItem? AtEnd => _items.FirstOrDefault(item => item.Position >= Length);

    internal ConveyorItem Add(Pallet pallet)
    {
        var item = new ConveyorItem(pallet);
        _items.Add(item);
        return item;
    }

    internal void Remove(ConveyorItem item) => _items.Remove(item);

    internal void Advance(double seconds)
    {
        if (State != ConveyorState.Running) return;

        var limit = Length;
        foreach (var item in _items.OrderByDescending(item => item.Position))
        {
            item.Position = Math.Max(item.Position, Math.Min(item.Position + Motion.ConveyorSpeed * seconds, limit));
            limit = Math.Max(0, item.Position - Motion.ConveyorSpacing);
        }
    }
}
