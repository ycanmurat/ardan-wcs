using Ardan.Core.Alarms;
using Ardan.Core.Contracts;
using Ardan.Core.Fleet;
using Ardan.Core.Orders;
using Ardan.Core.Stock;

namespace Ardan.Core.Tests;

public class OrderFlowTests
{
    [Fact]
    public void Outbound_order_is_picked_and_sent_to_the_dock()
    {
        var store = TestStore.Create();
        TestStore.Put(store, 1, 3, TestStore.Pallet("P1"), TestStore.Pallet("P2"));

        var result = store.CreateOrder(new NewOrder(OrderType.Outbound, "GF-1001", 2, 2, Priority.Normal, 30));
        TestStore.Run(store, TimeSpan.FromMinutes(10), () => store.FindOrder(result.Ref!)?.Status == OrderStatus.Completed);

        var order = store.FindOrder(result.Ref!)!;
        Assert.True(result.Ok);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(0, store.Grid.StoredCount);
        Assert.Empty(store.Jobs);
    }

    [Fact]
    public void Inbound_order_is_stored_in_one_lane()
    {
        var store = TestStore.Create();

        var result = store.CreateOrder(new NewOrder(OrderType.Inbound, "GF-2001", 3, 1, Priority.Normal, 60));
        TestStore.Run(store, TimeSpan.FromMinutes(15), () => store.FindOrder(result.Ref!)?.Status == OrderStatus.Completed);

        Assert.Equal(OrderStatus.Completed, store.FindOrder(result.Ref!)!.Status);
        Assert.Equal(3, store.Grid.StoredCount);
        Assert.Single(store.Grid.Lanes, lane => lane.Count > 0);
    }

    [Fact]
    public void Outbound_order_without_free_stock_is_refused()
    {
        var store = TestStore.Create();
        TestStore.Put(store, 0, 1, TestStore.Pallet("P1"));

        var result = store.CreateOrder(new NewOrder(OrderType.Outbound, "GF-1001", 2, 1, Priority.Normal, 30));

        Assert.False(result.Ok);
        Assert.Contains("Only 1 pallet", result.Message);
    }

    [Fact]
    public void A_stopped_taxi_waits_for_a_reset_and_then_carries_on()
    {
        var store = TestStore.Create(taxis: 1);
        TestStore.Put(store, 2, 6, TestStore.Pallet("P1"));
        var order = store.CreateOrder(new NewOrder(OrderType.Outbound, "GF-1001", 1, 1, Priority.Normal, 30)).Ref!;
        var taxi = store.Taxis[0];

        TestStore.Run(store, TimeSpan.FromMinutes(3), () => taxi.State == TaxiState.Driving);
        store.StopTaxi(taxi);
        var stoppedAt = (taxi.Level, taxi.X);
        TestStore.Run(store, TimeSpan.FromSeconds(30));

        Assert.Equal(stoppedAt, (taxi.Level, taxi.X));
        Assert.Contains(store.Alarms.Active, alarm => alarm.Code == "TX-OBS" && alarm.Action == AlarmAction.ResetTaxi);

        Assert.True(store.ResetTaxi(taxi.Id).Ok);
        TestStore.Run(store, TimeSpan.FromMinutes(5), () => store.FindOrder(order)?.Status == OrderStatus.Completed);

        Assert.Equal(OrderStatus.Completed, store.FindOrder(order)!.Status);
        Assert.DoesNotContain(store.Alarms.Active, alarm => alarm.Code == "TX-OBS");
    }

    [Fact]
    public void Cancelling_an_order_frees_its_lanes()
    {
        var store = TestStore.Create();
        foreach (var taxi in store.Taxis) store.SetInService(taxi.Id, false);
        TestStore.Put(store, 0, 2, TestStore.Pallet("P1"));
        TestStore.Put(store, 1, 2, TestStore.Pallet("P2", batch: "B2"));
        var order = store.CreateOrder(new NewOrder(OrderType.Outbound, "GF-1001", 2, 1, Priority.High, 30)).Ref!;
        TestStore.Run(store, TimeSpan.FromSeconds(5));
        Assert.All(store.Grid.Lanes.Where(lane => lane.Count > 0), lane => Assert.True(lane.IsLocked));

        var result = store.CancelOrder(order);

        Assert.True(result.Ok);
        Assert.Equal(OrderStatus.Cancelled, store.FindOrder(order)!.Status);
        Assert.All(store.Grid.Lanes, lane => Assert.False(lane.IsLocked));
        Assert.Equal(2, store.FreeStock("GF-1001"));
    }

    [Fact]
    public void A_rejected_pallet_goes_back_in_after_a_fix()
    {
        var store = TestStore.Create();
        store.ProfileFaultRate = 1;
        var order = store.CreateOrder(new NewOrder(OrderType.Inbound, "GF-3001", 1, 1, Priority.Normal, 60)).Ref!;
        TestStore.Run(store, TimeSpan.FromMinutes(2), () => store.Alarms.Active.Any(alarm => alarm.Action == AlarmAction.Reinduct));

        var alarm = store.Alarms.Active.Single(a => a.Action == AlarmAction.Reinduct);
        Assert.Equal(OrderStatus.Exception, store.FindOrder(order)!.Status);

        store.ProfileFaultRate = 0;
        Assert.True(store.RunAlarmAction(alarm.Id).Ok);
        TestStore.Run(store, TimeSpan.FromMinutes(8), () => store.FindOrder(order)?.Status == OrderStatus.Completed);

        Assert.Equal(OrderStatus.Completed, store.FindOrder(order)!.Status);
        Assert.Equal(PalletStatus.Stored, store.Pallets[alarm.Subject!].Status);
        Assert.Empty(store.Alarms.Active);
    }
}
