using Ardan.Core.Contracts;
using Ardan.Core.Fleet;
using Ardan.Core.Orders;

namespace Ardan.Core.Tests;

public class LiveViewTests
{
    [Fact]
    public void A_taxi_shows_its_route_and_how_far_it_is_with_the_pallet()
    {
        var store = TestStore.Create(taxis: 1);
        TestStore.Put(store, 1, 4, TestStore.Pallet("P1"), TestStore.Pallet("P2"));
        store.CreateOrder(new NewOrder(OrderType.Outbound, "GF-1001", 1, 1, Priority.Normal, 30));
        var taxi = store.Taxis[0];

        TestStore.Run(store, TimeSpan.FromMinutes(3), () => taxi.State == TaxiState.Loading);
        var loading = new LiveSnapshots().Next(store).Taxis.Single();
        TestStore.Run(store, TimeSpan.FromMinutes(1), () => taxi.State == TaxiState.Driving);
        var driving = new LiveSnapshots().Next(store).Taxis.Single();

        Assert.Equal(TaxiState.Loading, loading.State);
        Assert.Equal("L1·B04", loading.From);
        Assert.Equal("Outfeed", loading.To);
        Assert.InRange(loading.Handling!.Value, 0.001, 0.5);
        Assert.NotNull(driving.PalletId);
        Assert.Null(driving.Handling);
    }
}
