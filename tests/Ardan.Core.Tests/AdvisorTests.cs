using Ardan.Core.Contracts;
using Ardan.Core.Orders;
using Ardan.Core.Stock;

namespace Ardan.Core.Tests;

public class AdvisorTests
{
    [Fact]
    public void An_empty_store_suggests_restocking_fast_movers()
    {
        var store = TestStore.Create();

        var suggestions = Advisor.Suggest(store);

        Assert.Equal(3, suggestions.Count);
        Assert.All(suggestions, suggestion =>
        {
            Assert.Equal(SuggestionKind.Restock, suggestion.Kind);
            Assert.Equal(OrderType.Inbound, suggestion.Order!.Type);
            Assert.Equal(Velocity.A, store.SkuOf(suggestion.Order.SkuCode).Velocity);
        });
    }

    [Fact]
    public void Stock_close_to_its_best_before_is_offered_for_dispatch()
    {
        var store = TestStore.Create();
        TestStore.Put(store, 0, 2, TestStore.Pallet("P1", sku: "GF-3001", expiryDays: 2), TestStore.Pallet("P2", sku: "GF-3001", expiryDays: 2));

        var clear = Advisor.Suggest(store).Single(suggestion => suggestion.Kind == SuggestionKind.Clear);

        Assert.Equal(new NewOrder(OrderType.Outbound, "GF-3001", 2, 1, Priority.High, 30), clear.Order);
        Assert.True(store.CreateOrder(clear.Order!).Ok);
        Assert.DoesNotContain(Advisor.Suggest(store), suggestion => suggestion.Kind == SuggestionKind.Clear);
    }

    [Fact]
    public void An_order_close_to_its_due_time_can_be_rushed()
    {
        var store = TestStore.Create();
        foreach (var taxi in store.Taxis) store.SetInService(taxi.Id, false);
        TestStore.Put(store, 2, 6, TestStore.Pallet("P1"));
        var order = store.CreateOrder(new NewOrder(OrderType.Outbound, "GF-1001", 1, 1, Priority.Normal, 15)).Ref!;

        TestStore.Run(store, TimeSpan.FromMinutes(6));
        var rush = Advisor.Suggest(store)[0];

        Assert.Equal(SuggestionKind.Rush, rush.Kind);
        Assert.Equal(order, rush.OrderId);
        Assert.True(store.SetPriority(order, Priority.Urgent).Ok);
        Assert.DoesNotContain(Advisor.Suggest(store), suggestion => suggestion.Kind == SuggestionKind.Rush);
    }

    [Fact]
    public void A_short_order_asks_for_stock_until_it_is_on_its_way()
    {
        var store = TestStore.Create();
        var shortOrder = new Order
        {
            Id = "OUT-1",
            Type = OrderType.Outbound,
            Reference = "SO-1",
            SkuCode = "GF-2001",
            Quantity = 3,
            Status = OrderStatus.Short,
            CreatedAt = TestStore.Start,
            DueAt = TestStore.Start.AddHours(1),
        };
        store.Load(new[] { shortOrder });

        var unblock = Advisor.Suggest(store)[0];

        Assert.Equal(SuggestionKind.Unblock, unblock.Kind);
        Assert.Equal(new NewOrder(OrderType.Inbound, "GF-2001", 3, 1, Priority.High, 30), unblock.Order);
        Assert.True(store.CreateOrder(unblock.Order!).Ok);
        Assert.DoesNotContain(Advisor.Suggest(store), suggestion => suggestion.Kind == SuggestionKind.Unblock);
    }
}
