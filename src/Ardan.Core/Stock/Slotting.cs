using Ardan.Core.Layout;

namespace Ardan.Core.Stock;

/// <summary>Storage rules: one batch per lane, fast movers close to the lifts, oldest stock out first.</summary>
public static class Slotting
{
    public static Lane? LaneFor(StorageGrid grid, Pallet pallet, Velocity velocity)
    {
        var sameBatch = grid.Lanes
            .Where(lane => (!lane.IsEmpty || lane.Incoming.Count > 0) && lane.CanReceive(pallet))
            .MinBy(lane => AccessSeconds(grid.Layout, lane.Id));

        if (sameBatch is not null)
            return sameBatch;

        var target = TargetSeconds(grid.Layout, velocity);
        return grid.Lanes
            .Where(lane => lane.IsEmpty && !lane.IsBusy)
            .MinBy(lane => Math.Abs(AccessSeconds(grid.Layout, lane.Id) - target));
    }

    public static Lane? NextToRetrieve(StorageGrid grid, string skuCode) =>
        grid.Lanes
            .Where(lane => !lane.IsBusy && lane.Front?.SkuCode == skuCode)
            .OrderBy(lane => lane.Front!.Expiry)
            .ThenBy(lane => AccessSeconds(grid.Layout, lane.Id))
            .FirstOrDefault();

    public static double AccessSeconds(SiteLayout layout, LaneId lane) =>
        lane.Bay * layout.BayPitchMetres / Motion.TaxiEmptySpeed
        + lane.Level * layout.LevelHeightMetres / Motion.LiftSpeed;

    private static double TargetSeconds(SiteLayout layout, Velocity velocity)
    {
        var farthest = AccessSeconds(layout, new LaneId(layout.Levels - 1, layout.Bays));
        return velocity switch
        {
            Velocity.A => 0,
            Velocity.B => farthest / 2,
            _ => farthest,
        };
    }
}
