using System.Text.Json.Serialization;

namespace Ardan.Core.Orders;

[JsonConverter(typeof(JsonStringEnumConverter<OrderType>))]
public enum OrderType { Inbound, Outbound }

[JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))]
public enum OrderStatus { Queued, InProgress, Short, Exception, Completed, Cancelled }

[JsonConverter(typeof(JsonStringEnumConverter<Priority>))]
public enum Priority { Normal, High, Urgent }

/// <summary>A request from the ERP: receive pallets into store, or send pallets to a dock.</summary>
public sealed class Order
{
    public required string Id { get; init; }
    public required OrderType Type { get; init; }
    public required string Reference { get; init; }
    public required string SkuCode { get; init; }
    public required int Quantity { get; init; }
    public int Dock { get; init; } = 1;
    public Priority Priority { get; set; }
    public OrderStatus Status { get; set; }
    public string Source { get; init; } = "ERP";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset DueAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Batch { get; init; }
    public DateOnly? Expiry { get; init; }
    public int Done { get; set; }
    public int InFlight { get; set; }
    public int Arrived { get; set; }
    public int Rejected { get; set; }
    public int Returned { get; set; }
    public string? Note { get; set; }

    public bool IsOpen =>
        Status is OrderStatus.Queued or OrderStatus.InProgress or OrderStatus.Short or OrderStatus.Exception;

    public int StillToStart => Quantity - Done - InFlight;

    public bool IsLate(DateTimeOffset now) => IsOpen && now > DueAt;
}
