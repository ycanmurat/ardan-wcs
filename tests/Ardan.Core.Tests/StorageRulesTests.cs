using Ardan.Core.Layout;
using Ardan.Core.Stock;

namespace Ardan.Core.Tests;

public class StorageRulesTests
{
    [Fact]
    public void Lane_gives_back_the_last_pallet_first()
    {
        var lane = new Lane(new LaneId(0, 1), depth: 3);
        lane.Push(TestStore.Pallet("P1"));
        lane.Push(TestStore.Pallet("P2"));

        Assert.Equal("P2", lane.Pop().Id);
        Assert.Equal("P1", lane.Front?.Id);
    }

    [Fact]
    public void Lane_takes_only_one_batch()
    {
        var lane = new Lane(new LaneId(0, 1), depth: 3);
        lane.Push(TestStore.Pallet("P1", batch: "B1"));

        Assert.True(lane.CanReceive(TestStore.Pallet("P2", batch: "B1")));
        Assert.False(lane.CanReceive(TestStore.Pallet("P3", batch: "B2")));
        Assert.False(lane.CanReceive(TestStore.Pallet("P4", sku: "GF-2001", batch: "B1")));
    }

    [Fact]
    public void Fast_movers_are_stored_next_to_the_lifts()
    {
        var grid = new StorageGrid(TestStore.Small);

        var lane = Slotting.LaneFor(grid, TestStore.Pallet("P1"), Velocity.A);

        Assert.Equal(new LaneId(0, 1), lane?.Id);
    }

    [Fact]
    public void Slow_movers_are_stored_far_from_the_lifts()
    {
        var grid = new StorageGrid(TestStore.Small);

        var lane = Slotting.LaneFor(grid, TestStore.Pallet("P1"), Velocity.C);

        Assert.Equal(new LaneId(TestStore.Small.Levels - 1, TestStore.Small.Bays), lane?.Id);
    }

    [Fact]
    public void A_pallet_joins_a_lane_of_its_own_batch_first()
    {
        var grid = new StorageGrid(TestStore.Small);
        grid[new LaneId(2, 4)].Push(TestStore.Pallet("P1", batch: "B7"));

        var lane = Slotting.LaneFor(grid, TestStore.Pallet("P2", batch: "B7"), Velocity.A);

        Assert.Equal(new LaneId(2, 4), lane?.Id);
    }

    [Fact]
    public void The_oldest_stock_leaves_first()
    {
        var grid = new StorageGrid(TestStore.Small);
        grid[new LaneId(0, 1)].Push(TestStore.Pallet("Fresh", batch: "B2", expiryDays: 30));
        grid[new LaneId(2, 6)].Push(TestStore.Pallet("Older", batch: "B1", expiryDays: 5));

        var lane = Slotting.NextToRetrieve(grid, "GF-1001");

        Assert.Equal("Older", lane?.Front?.Id);
    }

    [Fact]
    public void A_busy_lane_is_skipped()
    {
        var grid = new StorageGrid(TestStore.Small);
        var busy = grid[new LaneId(0, 1)];
        busy.Push(TestStore.Pallet("P1"));
        busy.Lock("J-1");

        Assert.Null(Slotting.NextToRetrieve(grid, "GF-1001"));
    }
}
