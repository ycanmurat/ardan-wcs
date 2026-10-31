using System.Text.Json.Serialization;
using Ardan.Core.Alarms;
using Ardan.Core.Engine;
using Ardan.Core.Fleet;
using Ardan.Core.Orders;
using Ardan.Core.Stock;

namespace Ardan.Core.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<HealthLevel>))]
public enum HealthLevel { Running, Attention }

[JsonConverter(typeof(JsonStringEnumConverter<SuggestionKind>))]
public enum SuggestionKind { Rush, Unblock, Clear, Restock }

public sealed record SystemHealth(HealthLevel Level, string Summary);

public sealed record LayoutView(
    int Levels,
    int Bays,
    int LaneDepth,
    double BayPitchMetres,
    double LevelHeightMetres,
    double PalletPitchMetres,
    IReadOnlyList<string> Lifts,
    string Company,
    string Site);

public sealed record KpiSet(
    int Stored,
    int Capacity,
    double Occupancy,
    int InLastHour,
    int OutLastHour,
    int ReadyMachines,
    int Machines,
    double AverageCycleSeconds,
    int OpenOrders,
    int DueSoon,
    int Late,
    int CriticalAlarms,
    int WarningAlarms);

public sealed record TaxiView(
    string Id,
    TaxiState State,
    int Level,
    double X,
    double Battery,
    string? PalletId,
    string? SkuName,
    string? JobId,
    JobKind? JobKind,
    string? From,
    string? To,
    double? Handling,
    string? LiftId,
    double SpeedMs,
    double MotorTempC,
    int Signal,
    double DistanceKm,
    bool InService,
    bool ChargeHold,
    string? FaultCode,
    string Activity);

public sealed record LiftView(
    string Id,
    int Shaft,
    double Position,
    LiftState State,
    string? TaxiId,
    bool Loaded,
    int Waiting,
    string? FaultCode,
    string Activity);

public sealed record ConveyorItemView(string PalletId, string SkuCode, double Position);

public sealed record ConveyorView(
    string Id,
    string Name,
    ConveyorState State,
    double Length,
    IReadOnlyList<ConveyorItemView> Items,
    string? FaultCode);

public sealed record LaneView(
    int Level,
    int Bay,
    int Count,
    int Depth,
    string? SkuCode,
    string? SkuName,
    string? Batch,
    DateOnly? Expiry,
    bool Busy,
    int Version);

public sealed record AlarmView(
    string Id,
    string Code,
    Severity Severity,
    AlarmState State,
    string Source,
    string Title,
    string What,
    string Cause,
    IReadOnlyList<string> Steps,
    AlarmAction Action,
    string? ActionLabel,
    DateTimeOffset RaisedAt,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? ClearedAt,
    string? ClearedBy);

public sealed record EventView(long Seq, DateTimeOffset Time, EventKind Kind, string Text, string? Ref);

public sealed record OrderCounts(int Open, int Inbound, int Outbound, int Exceptions);

public sealed record ClimateView(double TemperatureC, double MinC, double MaxC);

public sealed record GoalView(GoalKind Kind, string Title, int Tier, double Progress, double Target, int Reward);

public sealed record ShiftView(
    DateTimeOffset StartedAt,
    int Points,
    int Earned,
    int Level,
    int LevelFrom,
    int LevelTo,
    double Stars,
    int OrdersRated,
    int OrdersOnTime,
    int GoalsDone,
    IReadOnlyList<GoalView> Goals,
    IReadOnlyList<ScoreEvent> Events);

/// <summary>Work worth doing now. Rush raises an order to urgent; the other kinds create the order they carry.</summary>
public sealed record SuggestionView(
    string Id,
    SuggestionKind Kind,
    string Title,
    string Reason,
    int Reward,
    NewOrder? Order,
    string? OrderId);

public sealed record LiveSnapshot(
    long Version,
    DateTimeOffset Time,
    double Speed,
    SystemHealth Health,
    KpiSet Kpis,
    IReadOnlyList<ThroughputPoint> Throughput,
    IReadOnlyList<TaxiView> Taxis,
    IReadOnlyList<LiftView> Lifts,
    IReadOnlyList<ConveyorView> Conveyors,
    IReadOnlyList<LaneView> ChangedLanes,
    IReadOnlyList<AlarmView> ActiveAlarms,
    IReadOnlyList<EventView> Events,
    OrderCounts Orders,
    ClimateView Climate,
    ShiftView Shift,
    IReadOnlyList<SuggestionView> Suggestions);

public sealed record OrderView(
    string Id,
    OrderType Type,
    string Reference,
    string SkuCode,
    string SkuName,
    int Quantity,
    int Done,
    int InFlight,
    int Rejected,
    int Dock,
    Priority Priority,
    OrderStatus Status,
    string Source,
    DateTimeOffset CreatedAt,
    DateTimeOffset DueAt,
    DateTimeOffset? CompletedAt,
    string? Note);

public sealed record SkuView(
    string Code,
    string Name,
    string Category,
    Velocity Velocity,
    int Pallets,
    int Lanes,
    int Free,
    DateOnly? NextExpiry,
    int CasesPerPallet);

public sealed record PalletView(
    string Id,
    string SkuCode,
    string SkuName,
    string Batch,
    DateOnly Expiry,
    int WeightKg,
    int HeightMm,
    PalletStatus Status,
    string Location,
    string? OrderId,
    DateTimeOffset ReceivedAt);

public sealed record MovementView(DateTimeOffset Time, MovementKind Kind, string From, string To, string? By);

public sealed record PalletDetail(PalletView Pallet, IReadOnlyList<MovementView> History);

public sealed record LaneDetail(LaneView Lane, IReadOnlyList<PalletView> Pallets);

public sealed record SearchHit(string Kind, string Id, string Title, string Detail, string Link);
