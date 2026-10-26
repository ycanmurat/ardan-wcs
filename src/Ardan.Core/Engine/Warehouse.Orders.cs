using Ardan.Core.Layout;
using Ardan.Core.Orders;
using Ardan.Core.Stock;

namespace Ardan.Core.Engine;

public sealed partial class Warehouse
{
    private const int ClosedOrdersKept = 80;

    /// <summary>Gives a fresh start some work: two outbound orders and one delivery.</summary>
    public void StartWork()
    {
        if (_orders.Any(order => order.IsOpen)) return;
        if (ErpOutbound() is { } first) AddOrder(first);
        if (ErpOutbound() is { } second) AddOrder(second);
        AddOrder(ErpInbound());
    }

    private void FeedOrders(double seconds)
    {
        if (!OrderFeedEnabled) return;
        _nextOrderIn -= seconds;
        if (_nextOrderIn > 0) return;
        _nextOrderIn = 30 + _random.NextDouble() * 30;

        var arriving = _orders.Count(order => order.IsOpen && order.Type == OrderType.Inbound && order.Arrived < order.Quantity);
        var openOutbound = _orders.Count(order => order.IsOpen && order.Type == OrderType.Outbound);
        var inboundShare = Grid.Occupancy switch
        {
            > 0.78 => 0.2,
            < 0.62 => 0.8,
            _ => 0.5,
        };

        var inbound = _random.NextDouble() < inboundShare;
        if (inbound && arriving >= 2) inbound = false;
        if (!inbound && openOutbound >= 7)
        {
            if (arriving >= 2) return;
            inbound = true;
        }

        var order = inbound ? ErpInbound() : ErpOutbound();
        if (order is not null) AddOrder(order);
    }

    private Order ErpInbound() =>
        NewInbound(PickSku(_skus.Values), 2 + _random.Next(5), Priority.Normal, "ERP", 40 + _random.Next(30));

    private Order? ErpOutbound()
    {
        var candidates = _skus.Values.Where(sku => FreeStock(sku.Code) > 0).ToList();
        if (candidates.Count == 0) return null;

        var sku = PickSku(candidates);
        var quantity = Math.Min(1 + _random.Next(4), FreeStock(sku.Code));
        var roll = _random.NextDouble();
        var priority = roll < 0.06 ? Priority.Urgent : roll < 0.26 ? Priority.High : Priority.Normal;
        return NewOutbound(sku, quantity, 1 + _random.Next(Limits.Docks), priority, "ERP", 15 + _random.Next(60));
    }

    private Order NewInbound(Sku sku, int quantity, Priority priority, string source, int dueInMinutes) => new()
    {
        Id = $"IN-{++_inboundNumber}",
        Type = OrderType.Inbound,
        Reference = $"ASN-{++_asnNumber}",
        SkuCode = sku.Code,
        Quantity = quantity,
        Priority = priority,
        Status = OrderStatus.Queued,
        Source = source,
        CreatedAt = Now,
        DueAt = Now.AddMinutes(dueInMinutes),
        Batch = NewBatch(Now),
        Expiry = DateOnly.FromDateTime(Now.AddDays(sku.ShelfLifeDays).Date),
    };

    private Order NewOutbound(Sku sku, int quantity, int dock, Priority priority, string source, int dueInMinutes) => new()
    {
        Id = $"OUT-{++_outboundNumber}",
        Type = OrderType.Outbound,
        Reference = $"SO-{++_salesOrderNumber}",
        SkuCode = sku.Code,
        Quantity = quantity,
        Dock = dock,
        Priority = priority,
        Status = OrderStatus.Queued,
        Source = source,
        CreatedAt = Now,
        DueAt = Now.AddMinutes(dueInMinutes),
    };

    private void AddOrder(Order order)
    {
        _orders.Add(order);
        _journal.OrderChanged(order);

        var what = order.Type == OrderType.Inbound
            ? $"{order.Quantity} × {NameOf(order.SkuCode)} arriving"
            : $"{order.Quantity} × {NameOf(order.SkuCode)} to dock {order.Dock}";
        Events.Add(Now, EventKind.Order, $"{order.Id} from {order.Source}: {what}", order.Id);

        var closed = _orders
            .Where(o => !o.IsOpen)
            .OrderByDescending(o => o.CompletedAt)
            .Skip(ClosedOrdersKept)
            .ToList();
        foreach (var old in closed) _orders.Remove(old);
    }

    private Sku PickSku(IEnumerable<Sku> skus)
    {
        var list = skus.ToList();
        var roll = _random.NextDouble() * list.Sum(Weight);
        foreach (var sku in list)
        {
            roll -= Weight(sku);
            if (roll <= 0) return sku;
        }
        return list[^1];

        static double Weight(Sku sku) => sku.Velocity switch
        {
            Velocity.A => 3,
            Velocity.B => 2,
            _ => 1,
        };
    }

    private string NewBatch(DateTimeOffset date) => $"B{date:yyMMdd}-{_random.Next(10, 99)}";
}
