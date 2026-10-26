using System.Text.Json.Serialization;
using Ardan.Core.Alarms;
using Ardan.Core.Orders;
using Ardan.Core.Stock;

namespace Ardan.Core.Engine;

[JsonConverter(typeof(JsonStringEnumConverter<MovementKind>))]
public enum MovementKind { Received, Rejected, Reinducted, Returned, Stored, Retrieved, Dispatched }

/// <summary>One step in a pallet's history, kept for traceability.</summary>
public sealed record Movement(
    string PalletId,
    DateTimeOffset Time,
    MovementKind Kind,
    string From,
    string To,
    string? By);

/// <summary>Receives every change worth keeping. The API writes them to the database.</summary>
public interface IJournal
{
    void PalletChanged(Pallet pallet);
    void OrderChanged(Order order);
    void Moved(Movement movement);
    void AlarmChanged(Alarm alarm);
}

/// <summary>A journal that keeps nothing. Used in tests.</summary>
public sealed class NullJournal : IJournal
{
    public static NullJournal Instance { get; } = new();

    public void PalletChanged(Pallet pallet) { }
    public void OrderChanged(Order order) { }
    public void Moved(Movement movement) { }
    public void AlarmChanged(Alarm alarm) { }
}
