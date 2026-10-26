using System.Text.Json.Serialization;

namespace Ardan.Core.Alarms;

[JsonConverter(typeof(JsonStringEnumConverter<Severity>))]
public enum Severity { Info, Warning, Critical }

[JsonConverter(typeof(JsonStringEnumConverter<AlarmState>))]
public enum AlarmState { Active, Acknowledged, Cleared }

[JsonConverter(typeof(JsonStringEnumConverter<AlarmAction>))]
public enum AlarmAction { None, ResetTaxi, ResetLift, ResetConveyor, Reinduct }

/// <summary>An alarm with plain guidance: what happened, the likely cause and the steps to fix it.</summary>
public sealed class Alarm
{
    public required string Id { get; init; }
    public required string Code { get; init; }
    public required Severity Severity { get; init; }
    public required string Source { get; init; }
    public required string Title { get; init; }
    public required string What { get; init; }
    public required string Cause { get; init; }
    public required IReadOnlyList<string> Steps { get; init; }
    public AlarmAction Action { get; init; }
    public string? ActionLabel { get; init; }
    public string? Subject { get; init; }
    public DateTimeOffset RaisedAt { get; init; }
    public AlarmState State { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? ClearedAt { get; set; }
    public string? ClearedBy { get; set; }

    public bool IsActive => State != AlarmState.Cleared;
}
