using Ardan.Core.Engine;

namespace Ardan.Core.Alarms;

/// <summary>Active alarms and the most recent cleared ones.</summary>
public sealed class AlarmBoard(IJournal journal, int lastNumber = 0)
{
    private const int HistorySize = 200;
    private readonly List<Alarm> _active = [];
    private readonly LinkedList<Alarm> _cleared = new();
    private int _number = lastNumber;

    public IReadOnlyList<Alarm> Active => _active;
    public IEnumerable<Alarm> Cleared => _cleared;

    public event Action<Alarm>? Closed;

    public Alarm? Raise(
        string code,
        string source,
        DateTimeOffset now,
        IReadOnlyDictionary<string, string>? values = null,
        string? subject = null)
    {
        if (Find(code, source, subject) is not null) return null;

        var alarm = AlarmCatalog.Create($"AL-{++_number:0000}", code, source, now, values, subject);
        _active.Add(alarm);
        journal.AlarmChanged(alarm);
        return alarm;
    }

    public Alarm? Get(string id) => _active.FirstOrDefault(alarm => alarm.Id == id);

    public Alarm? Find(string code, string source, string? subject = null) =>
        _active.FirstOrDefault(alarm => alarm.Code == code && alarm.Source == source && alarm.Subject == subject);

    public bool Acknowledge(string id, DateTimeOffset now)
    {
        var alarm = Get(id);
        if (alarm is null || alarm.State != AlarmState.Active) return false;
        alarm.State = AlarmState.Acknowledged;
        alarm.AcknowledgedAt = now;
        journal.AlarmChanged(alarm);
        return true;
    }

    public void Clear(Alarm alarm, DateTimeOffset now, string clearedBy)
    {
        if (!_active.Remove(alarm)) return;
        alarm.State = AlarmState.Cleared;
        alarm.ClearedAt = now;
        alarm.ClearedBy = clearedBy;
        _cleared.AddFirst(alarm);
        if (_cleared.Count > HistorySize) _cleared.RemoveLast();
        journal.AlarmChanged(alarm);
        Closed?.Invoke(alarm);
    }

    public void Clear(string code, string source, DateTimeOffset now, string clearedBy, string? subject = null)
    {
        if (Find(code, source, subject) is { } alarm)
            Clear(alarm, now, clearedBy);
    }
}
