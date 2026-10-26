using Ardan.Core.Layout;

namespace Ardan.Core.Stock;

/// <summary>All lanes of the rack, addressed by level and bay.</summary>
public sealed class StorageGrid
{
    private readonly Lane[] _lanes;

    public StorageGrid(SiteLayout layout)
    {
        Layout = layout;
        _lanes = new Lane[layout.LaneCount];
        for (var level = 0; level < layout.Levels; level++)
        for (var bay = 1; bay <= layout.Bays; bay++)
            _lanes[Index(level, bay)] = new Lane(new LaneId(level, bay), layout.LaneDepth);
    }

    public SiteLayout Layout { get; }
    public IReadOnlyList<Lane> Lanes => _lanes;
    public Lane this[LaneId id] => _lanes[Index(id.Level, id.Bay)];
    public int StoredCount => _lanes.Sum(lane => lane.Count);
    public double Occupancy => (double)StoredCount / Layout.Capacity;

    public bool Contains(LaneId id) =>
        id.Level >= 0 && id.Level < Layout.Levels && id.Bay >= 1 && id.Bay <= Layout.Bays;

    public int StoredOf(string skuCode) =>
        _lanes.Where(lane => lane.Front?.SkuCode == skuCode).Sum(lane => lane.Count);

    private int Index(int level, int bay) => level * Layout.Bays + (bay - 1);
}
