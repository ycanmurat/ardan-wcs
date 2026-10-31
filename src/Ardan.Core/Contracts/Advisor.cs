using System.Globalization;
using Ardan.Core.Engine;
using Ardan.Core.Layout;
using Ardan.Core.Orders;
using Ardan.Core.Stock;

namespace Ardan.Core.Contracts;

/// <summary>Spots work worth doing now and offers it ready to go. Nothing changes until a person accepts it.</summary>
public static class Advisor
{
    private const int Shown = 3;
    private const int LowStock = 2;
    private const int ExpiringDays = 3;
    private const int ClearAtMost = 4;
    private static readonly TimeSpan RushWithin = TimeSpan.FromMinutes(10);

    public static IReadOnlyList<SuggestionView> Suggest(Warehouse w)
    {
        var open = w.Orders.Where(order => order.IsOpen).ToList();
        return Rush(w, open)
            .Concat(Unblock(w, open))
            .Concat(Clear(w, open))
            .Concat(Restock(w, open))
            .DistinctBy(suggestion => suggestion.Order?.SkuCode ?? suggestion.OrderId)
            .Take(Shown)
            .ToList();
    }

    private static IEnumerable<SuggestionView> Rush(Warehouse w, List<Order> open) =>
        open.Where(order => order.Type == OrderType.Outbound
                && order.Priority != Priority.Urgent
                && order.Status != OrderStatus.Short
                && order.Done < order.Quantity
                && order.DueAt > w.Now
                && order.DueAt - w.Now <= RushWithin)
            .OrderBy(order => order.DueAt)
            .Select(order => new SuggestionView(
                $"rush:{order.Id}",
                SuggestionKind.Rush,
                $"Rush {order.Id}",
                $"Due in {Minutes(order.DueAt - w.Now)} with {Pallets(order.Quantity - order.Done)} to go. Urgent orders get the next free Taxi.",
                Rewards.OnTime,
                null,
                order.Id));

    private static IEnumerable<SuggestionView> Unblock(Warehouse w, List<Order> open) =>
        open.Where(order => order.Type == OrderType.Outbound && order.Status == OrderStatus.Short && !Arriving(open, order.SkuCode))
            .OrderByDescending(order => order.Priority)
            .ThenBy(order => order.DueAt)
            .Select(order =>
            {
                var pallets = Math.Clamp(order.StillToStart, 2, Limits.MaxPalletsPerOrder);
                return Receive(
                    $"unblock:{order.SkuCode}",
                    SuggestionKind.Unblock,
                    $"Unblock {order.Id}",
                    $"No {ShortName(w.NameOf(order.SkuCode))} left in store for it. Receive {Pallets(pallets)} so it can finish.",
                    new NewOrder(OrderType.Inbound, order.SkuCode, pallets, 1, Priority.High, 30));
            });

    private static IEnumerable<SuggestionView> Clear(Warehouse w, List<Order> open)
    {
        var today = DateOnly.FromDateTime(w.Now.UtcDateTime);
        var soon = today.AddDays(ExpiringDays);
        foreach (var sku in w.Skus.Values.OrderBy(sku => sku.Code))
        {
            var expiring = w.Grid.Lanes
                .Where(lane => lane.Front is { } front && front.SkuCode == sku.Code && front.Expiry >= today && front.Expiry <= soon)
                .ToList();
            if (expiring.Count == 0 || Sending(open, sku.Code)) continue;

            var pallets = Math.Min(Math.Min(expiring.Sum(lane => lane.Count), w.FreeStock(sku.Code)), ClearAtMost);
            if (pallets == 0) continue;

            var dock = QuietestDock(open);
            var bestBefore = expiring.Min(lane => lane.Front!.Expiry).ToString("d MMM", CultureInfo.InvariantCulture);
            var order = new NewOrder(OrderType.Outbound, sku.Code, pallets, dock, Priority.High, 30);
            yield return new SuggestionView(
                $"clear:{sku.Code}",
                SuggestionKind.Clear,
                $"Clear old {ShortName(sku.Name).ToLowerInvariant()}",
                $"Stock is best before {bestBefore}. Send {Pallets(pallets)} to dock {dock} now, oldest first.",
                Rewards.ForOrder(order.Type, order.Quantity),
                order,
                null);
        }
    }

    private static IEnumerable<SuggestionView> Restock(Warehouse w, List<Order> open) =>
        w.Skus.Values
            .Where(sku => sku.Velocity != Velocity.C && !Arriving(open, sku.Code))
            .Select(sku => (Sku: sku, Free: w.FreeStock(sku.Code)))
            .Where(item => item.Free <= LowStock)
            .OrderBy(item => item.Sku.Velocity)
            .ThenBy(item => item.Free)
            .ThenBy(item => item.Sku.Code)
            .Select(item =>
            {
                var pallets = item.Sku.Velocity == Velocity.A ? 4 : 3;
                var mover = item.Sku.Velocity == Velocity.A ? "a fast mover" : "a steady mover";
                return Receive(
                    $"restock:{item.Sku.Code}",
                    SuggestionKind.Restock,
                    $"Restock {ShortName(item.Sku.Name).ToLowerInvariant()}",
                    item.Free == 0
                        ? $"No free stock left of {mover}. Receive {Pallets(pallets)} before orders run short."
                        : $"Only {Pallets(item.Free)} free of {mover}. Receive {Pallets(pallets)} before orders run short.",
                    new NewOrder(OrderType.Inbound, item.Sku.Code, pallets, 1, Priority.Normal, 60));
            });

    private static SuggestionView Receive(string id, SuggestionKind kind, string title, string reason, NewOrder order) =>
        new(id, kind, title, reason, Rewards.ForOrder(order.Type, order.Quantity), order, null);

    private static bool Arriving(List<Order> open, string skuCode) =>
        open.Any(order => order.Type == OrderType.Inbound && order.SkuCode == skuCode);

    private static bool Sending(List<Order> open, string skuCode) =>
        open.Any(order => order.Type == OrderType.Outbound && order.SkuCode == skuCode);

    private static int QuietestDock(List<Order> open) =>
        Enumerable.Range(1, Limits.Docks).MinBy(dock => open.Count(order => order.Type == OrderType.Outbound && order.Dock == dock));

    private static string ShortName(string name) => name.Split(',')[0];

    private static string Pallets(int count) => count == 1 ? "1 pallet" : $"{count} pallets";

    private static string Minutes(TimeSpan span) => $"{Math.Max(1, (int)Math.Ceiling(span.TotalMinutes))} min";
}
