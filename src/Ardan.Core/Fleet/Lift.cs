using System.Text.Json.Serialization;

namespace Ardan.Core.Fleet;

[JsonConverter(typeof(JsonStringEnumConverter<LiftState>))]
public enum LiftState { Idle, Moving, Boarding, Carrying, Leaving, Fault }

/// <summary>A request to carry one Taxi from one level to another.</summary>
public sealed record RideRequest(Taxi Taxi, int FromLevel, int ToLevel);

/// <summary>A vertical transfer unit (VTU). It carries a Taxi, loaded or empty, between levels.</summary>
public sealed class Lift(string id, int shaft)
{
    public string Id { get; } = id;
    public int Shaft { get; } = shaft;
    public double Position { get; internal set; }
    public LiftState State { get; internal set; } = LiftState.Idle;
    public RideRequest? Active { get; internal set; }
    public string? FaultCode { get; internal set; }
    public DateTimeOffset? FaultSince { get; internal set; }
    public int Waiting => Queue.Count;

    internal Queue<RideRequest> Queue { get; } = new();
    internal double Timer { get; set; }
    internal LiftState StateBeforeFault { get; set; }
    internal int Busy => Queue.Count + (Active is null ? 0 : 1);
}
