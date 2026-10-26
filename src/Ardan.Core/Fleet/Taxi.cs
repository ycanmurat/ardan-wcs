using System.Text.Json.Serialization;
using Ardan.Core.Layout;
using Ardan.Core.Orders;
using Ardan.Core.Stock;

namespace Ardan.Core.Fleet;

[JsonConverter(typeof(JsonStringEnumConverter<TaxiState>))]
public enum TaxiState
{
    Idle,
    Charging,
    Driving,
    WaitingForAisle,
    WaitingForLift,
    OnLift,
    Loading,
    Unloading,
    Fault,
    OutOfService,
}

/// <summary>A battery pallet shuttle. It drives along one level and rides a VTU to change level.</summary>
public sealed class Taxi(string id, int level)
{
    public string Id { get; } = id;
    public int Level { get; internal set; } = level;
    public double X { get; internal set; }
    public double Battery { get; internal set; } = 100;
    public TaxiState State { get; internal set; } = TaxiState.Idle;
    public Pallet? Load { get; internal set; }
    public Job? Job { get; internal set; }
    public Lift? Lift { get; internal set; }
    public double MotorTempC { get; internal set; } = 9;
    public int Signal { get; internal set; } = 4;
    public double DistanceMetres { get; internal set; }
    public string? FaultCode { get; internal set; }
    public DateTimeOffset? FaultSince { get; internal set; }
    public bool InService { get; internal set; } = true;
    public bool ChargeHold { get; internal set; }

    public double TravelSpeed => Load is null ? Motion.TaxiEmptySpeed : Motion.TaxiLoadedSpeed;
    public double SpeedMs => State == TaxiState.Driving ? TravelSpeed : 0;

    public bool CanTakeJob =>
        InService
        && FaultCode is null
        && Job is null
        && Lift is null
        && !RideRequested
        && !ChargeHold
        && Battery >= Limits.WorkBattery;

    internal Queue<Leg> Route { get; } = new();
    internal double? TransferLeft { get; set; }
    internal bool RideRequested { get; set; }
    internal TaxiState StateBeforeFault { get; set; }
    internal double SignalBackIn { get; set; }
}
