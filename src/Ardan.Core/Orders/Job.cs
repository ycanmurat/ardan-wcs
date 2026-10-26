using System.Text.Json.Serialization;
using Ardan.Core.Layout;
using Ardan.Core.Stock;

namespace Ardan.Core.Orders;

[JsonConverter(typeof(JsonStringEnumConverter<JobKind>))]
public enum JobKind { Putaway, Retrieval }

public enum JobStatus { Waiting, Assigned, Done, Cancelled }

public enum PlaceKind { Infeed, Outfeed, Lane }

/// <summary>A place where a pallet is picked up or set down. The infeed and outfeed sit on level 0 next to the lifts.</summary>
public readonly record struct Place(PlaceKind Kind, LaneId Lane)
{
    public static Place Infeed { get; } = new(PlaceKind.Infeed, default);
    public static Place Outfeed { get; } = new(PlaceKind.Outfeed, default);
    public static Place At(LaneId lane) => new(PlaceKind.Lane, lane);

    public int Level => Kind == PlaceKind.Lane ? Lane.Level : 0;
    public double X => Kind == PlaceKind.Lane ? Lane.Bay : 0;

    public override string ToString() => Kind switch
    {
        PlaceKind.Infeed => "the infeed",
        PlaceKind.Outfeed => "the outfeed",
        _ => Lane.ToString(),
    };
}

/// <summary>One pallet move for a Taxi: from the infeed into a lane, or from a lane to the outfeed.</summary>
public sealed class Job
{
    public required string Id { get; init; }
    public required JobKind Kind { get; init; }
    public required Pallet Pallet { get; init; }
    public required Place From { get; init; }
    public required Place To { get; init; }
    public Order? Order { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public JobStatus Status { get; set; }
    public string? TaxiId { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public Priority Priority => Order?.Priority ?? Priority.Normal;
}
