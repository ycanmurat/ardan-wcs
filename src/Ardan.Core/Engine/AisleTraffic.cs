namespace Ardan.Core.Engine;

/// <summary>Traffic rule: one Taxi at a time in the aisle of each level. Others wait at the dock beside the lifts.</summary>
internal sealed class AisleTraffic
{
    private readonly Dictionary<int, string> _owners = [];

    public bool TryEnter(int level, string taxiId)
    {
        if (_owners.TryGetValue(level, out var owner)) return owner == taxiId;
        _owners[level] = taxiId;
        return true;
    }

    public void Leave(int level, string taxiId)
    {
        if (_owners.TryGetValue(level, out var owner) && owner == taxiId)
            _owners.Remove(level);
    }

    public string? OwnerOf(int level) => _owners.GetValueOrDefault(level);
}
