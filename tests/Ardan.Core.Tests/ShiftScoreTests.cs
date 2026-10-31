using Ardan.Core.Contracts;
using Ardan.Core.Engine;
using Ardan.Core.Orders;

namespace Ardan.Core.Tests;

public class ShiftScoreTests
{
    [Fact]
    public void Dispatched_pallets_and_an_on_time_order_earn_points()
    {
        var store = TestStore.Create();
        TestStore.Put(store, 1, 3, TestStore.Pallet("P1"), TestStore.Pallet("P2"));

        var order = store.CreateOrder(new NewOrder(OrderType.Outbound, "GF-1001", 2, 2, Priority.Normal, 30)).Ref!;
        TestStore.Run(store, TimeSpan.FromMinutes(10), () => store.FindOrder(order)?.Status == OrderStatus.Completed);

        Assert.Equal(2 * Rewards.Dispatched + Rewards.OnTime, store.Score.Points);
        Assert.Equal(1, store.Score.OrdersOnTime);
        Assert.Equal(5, store.Score.Stars);
        Assert.Contains(store.Score.Latest(10), e => e.Kind == ScoreKind.Dispatched && e.Spot == "Dock 2");
    }

    [Fact]
    public void Stored_pallets_earn_points_at_their_lane()
    {
        var store = TestStore.Create();

        var order = store.CreateOrder(new NewOrder(OrderType.Inbound, "GF-2001", 2, 1, Priority.Normal, 60)).Ref!;
        TestStore.Run(store, TimeSpan.FromMinutes(10), () => store.FindOrder(order)?.Status == OrderStatus.Completed);

        var lane = store.Grid.Lanes.Single(lane => lane.Count > 0).Id.ToString();
        Assert.Equal(2, store.Score.Latest(10).Count(e => e.Kind == ScoreKind.Stored && e.Spot == lane));
        Assert.Equal(2, store.Score.Goals.Single(goal => goal.Kind == GoalKind.Store).Progress);
    }

    [Fact]
    public void A_late_order_costs_points_and_stars()
    {
        var store = TestStore.Create();
        foreach (var taxi in store.Taxis) store.SetInService(taxi.Id, false);
        TestStore.Put(store, 0, 1, TestStore.Pallet("P1"));
        var order = store.CreateOrder(new NewOrder(OrderType.Outbound, "GF-1001", 1, 1, Priority.Normal, 5)).Ref!;

        TestStore.Run(store, TimeSpan.FromMinutes(6));
        foreach (var taxi in store.Taxis) store.SetInService(taxi.Id, true);
        TestStore.Run(store, TimeSpan.FromMinutes(5), () => store.FindOrder(order)?.Status == OrderStatus.Completed);

        Assert.Equal(OrderStatus.Completed, store.FindOrder(order)!.Status);
        Assert.Contains(store.Score.Latest(10), e => e.Kind == ScoreKind.Penalty && e.Points == Rewards.Late);
        Assert.Equal(0, store.Score.OrdersOnTime);
        Assert.Equal(0, store.Score.Stars);
    }

    [Fact]
    public void An_order_already_late_when_the_shift_started_is_not_rated()
    {
        var store = TestStore.Create();
        TestStore.Put(store, 1, 3, TestStore.Pallet("P1"));
        store.Load(new[]
        {
            new Order
            {
                Id = "OUT-90001",
                Type = OrderType.Outbound,
                Reference = "SO-1",
                SkuCode = "GF-1001",
                Quantity = 1,
                Status = OrderStatus.Queued,
                CreatedAt = TestStore.Start.AddHours(-3),
                DueAt = TestStore.Start.AddHours(-2),
            },
        });

        TestStore.Run(store, TimeSpan.FromMinutes(10), () => store.FindOrder("OUT-90001")?.Status == OrderStatus.Completed);

        Assert.Equal(OrderStatus.Completed, store.FindOrder("OUT-90001")!.Status);
        Assert.DoesNotContain(store.Score.Latest(10), e => e.Kind == ScoreKind.Penalty);
        Assert.Equal(0, store.Score.OrdersRated);
        Assert.Equal(Rewards.Dispatched, store.Score.Points);
    }

    [Fact]
    public void A_quick_reset_earns_a_bonus_and_waiting_for_remote_support_costs_points()
    {
        var store = TestStore.Create(taxis: 2);
        var (quick, slow) = (store.Taxis[0], store.Taxis[1]);
        store.StopTaxi(quick);
        store.StopTaxi(slow);

        TestStore.Run(store, TimeSpan.FromSeconds(20));
        Assert.True(store.ResetTaxi(quick.Id).Ok);
        TestStore.Run(store, TimeSpan.FromMinutes(5));

        var events = store.Score.Latest(10).ToList();
        Assert.Contains(events, e => e.Kind == ScoreKind.Fix && e.Points == Rewards.Fix && e.Spot == quick.Id);
        Assert.Contains(events, e => e.Kind == ScoreKind.Fix && e.Points == Rewards.FastFix);
        Assert.Contains(events, e => e.Kind == ScoreKind.Penalty && e.Points == Rewards.MissedFix && e.Spot == slow.Id);
        Assert.Equal(Rewards.Fix + Rewards.FastFix + Rewards.MissedFix, store.Score.Points);
    }

    [Fact]
    public void Reaching_a_goal_pays_its_reward_and_raises_the_target()
    {
        var store = TestStore.Create(taxis: 2);
        var goal = store.Score.Goals.Single(g => g.Kind == GoalKind.Fix);
        var reward = goal.Reward;

        foreach (var taxi in store.Taxis)
        {
            store.StopTaxi(taxi);
            store.ResetTaxi(taxi.Id);
        }

        Assert.Equal(2, goal.Tier);
        Assert.Equal(3, goal.Target);
        Assert.Equal(1, store.Score.GoalsDone);
        Assert.Equal(2 * (Rewards.Fix + Rewards.FastFix) + reward, store.Score.Points);
        Assert.Contains(store.Score.Latest(10), e => e.Kind == ScoreKind.Goal && e.Points == reward);
    }

    [Fact]
    public void A_stop_restarts_the_uptime_goal()
    {
        var store = TestStore.Create();
        var uptime = store.Score.Goals.Single(goal => goal.Kind == GoalKind.Uptime);

        TestStore.Run(store, TimeSpan.FromMinutes(9));
        store.StopTaxi(store.Taxis[0]);
        TestStore.Run(store, TimeSpan.FromSeconds(2));
        store.ResetTaxi(store.Taxis[0].Id);

        Assert.Equal(1, uptime.Tier);
        Assert.Equal(0, uptime.Progress);

        TestStore.Run(store, TimeSpan.FromMinutes(11));

        Assert.Equal(2, uptime.Tier);
    }

    [Fact]
    public void Earning_points_raises_the_level()
    {
        var store = TestStore.Create(taxis: 6);

        foreach (var taxi in store.Taxis)
        {
            store.StopTaxi(taxi);
            store.ResetTaxi(taxi.Id);
        }

        Assert.True(store.Score.Earned >= ShiftScore.PointsFor(2));
        Assert.Equal(2, store.Score.Level);
        Assert.Contains(store.Score.Latest(20), e => e.Kind == ScoreKind.Level && e.Text == "Level 2 reached");
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(249, 1)]
    [InlineData(250, 2)]
    [InlineData(749, 2)]
    [InlineData(750, 3)]
    [InlineData(2500, 5)]
    public void Each_level_needs_more_points_than_the_last(int earned, int level) =>
        Assert.Equal(level, ShiftScore.LevelFor(earned));
}
