using Ardan.Core.Engine;
using Ardan.Core.Layout;
using Ardan.Core.Orders;
using Ardan.Core.Stock;

namespace Ardan.Core.Tests;

public class SimulationTests
{
    [Fact]
    public void Two_busy_hours_keep_the_store_consistent()
    {
        var plan = DemoSite.Stock(SiteLayout.Default, TestStore.Start, new Random(11));
        var seeds = new IdSeeds(Pallet: plan.LastPalletNumber);
        var store = new Warehouse(SiteLayout.Default, DemoSite.Skus, TestStore.Start, new Random(3), NullJournal.Instance, seeds);
        store.Load(plan.Pallets);
        store.StartWork();

        TestStore.Run(store, TimeSpan.FromHours(2));

        Assert.True(store.Orders.Count(order => order.Status == OrderStatus.Completed) >= 30);
        Assert.All(store.Taxis, taxi => Assert.True(taxi.DistanceMetres > 100, $"{taxi.Id} hardly moved"));

        foreach (var lane in store.Grid.Lanes)
        {
            Assert.True(lane.Pallets.Select(pallet => (pallet.SkuCode, pallet.Batch)).Distinct().Count() <= 1, $"{lane.Id} mixes batches");
            foreach (var jobId in lane.Incoming.Append(lane.LockedBy).OfType<string>())
                Assert.Contains(store.Jobs, job => job.Id == jobId);
        }

        var tracked = store.Pallets.Values.Count(pallet => pallet.Status != PalletStatus.Rejected);
        var onTaxis = store.Taxis.Count(taxi => taxi.Load is not null);
        var onConveyors = store.Infeed.Items.Count + store.Outfeed.Items.Count;
        Assert.Equal(tracked, store.Grid.StoredCount + onTaxis + onConveyors);
    }
}
