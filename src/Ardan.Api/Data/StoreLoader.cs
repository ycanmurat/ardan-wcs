using Ardan.Core.Alarms;
using Ardan.Core.Engine;
using Ardan.Core.Layout;
using Ardan.Core.Orders;
using Ardan.Core.Stock;
using Microsoft.EntityFrameworkCore;

namespace Ardan.Api.Data;

/// <summary>Creates the database on first run, fills it with the demo site, and builds the live model from it.</summary>
public static class StoreLoader
{
    private static readonly OrderStatus[] OpenStatuses =
        [OrderStatus.Queued, OrderStatus.InProgress, OrderStatus.Short, OrderStatus.Exception];

    public static async Task<Warehouse> LoadAsync(ArdanDb db, IJournal journal)
    {
        await db.Database.EnsureCreatedAsync();
        if (!await db.Skus.AnyAsync()) await SeedAsync(db);
        await CloseAlarmsFromLastRunAsync(db);

        var skus = (await db.Skus.AsNoTracking().ToListAsync()).Select(row => row.ToSku()).ToList();
        var stored = (await db.Pallets.AsNoTracking().Where(row => row.Status == PalletStatus.Stored).ToListAsync())
            .Select(row => row.ToPallet())
            .ToList();
        var open = (await db.Orders.AsNoTracking().Where(row => OpenStatuses.Contains(row.Status)).ToListAsync())
            .Select(row => row.ToOpenOrder())
            .ToList();

        var defaults = new IdSeeds();
        var seeds = new IdSeeds(
            Pallet: await HighestAsync(db.Pallets.Select(row => row.Id), defaults.Pallet),
            Inbound: await HighestAsync(db.Orders.Where(row => row.Type == OrderType.Inbound).Select(row => row.Id), defaults.Inbound),
            Outbound: await HighestAsync(db.Orders.Where(row => row.Type == OrderType.Outbound).Select(row => row.Id), defaults.Outbound),
            Alarm: await HighestAsync(db.Alarms.Select(row => row.Id), defaults.Alarm),
            Asn: await HighestAsync(db.Orders.Where(row => row.Type == OrderType.Inbound).Select(row => row.Reference), defaults.Asn),
            SalesOrder: await HighestAsync(db.Orders.Where(row => row.Type == OrderType.Outbound).Select(row => row.Reference), defaults.SalesOrder));

        var warehouse = new Warehouse(SiteLayout.Default, skus, DateTimeOffset.UtcNow, new Random(), journal, seeds);
        warehouse.Load(stored);
        warehouse.Load(open);
        warehouse.StartWork();
        return warehouse;
    }

    private static async Task SeedAsync(ArdanDb db)
    {
        var plan = DemoSite.Stock(SiteLayout.Default, DateTimeOffset.UtcNow, new Random());
        db.Skus.AddRange(DemoSite.Skus.Select(sku => sku.ToRow()));
        db.Pallets.AddRange(plan.Pallets.Select(pallet => pallet.ToRow()));
        db.Movements.AddRange(plan.Movements.Select(movement => movement.ToRow()));
        await db.SaveChangesAsync();
    }

    private static Task CloseAlarmsFromLastRunAsync(ArdanDb db)
    {
        var now = DateTimeOffset.UtcNow;
        return db.Alarms
            .Where(row => row.State != AlarmState.Cleared)
            .ExecuteUpdateAsync(set => set
                .SetProperty(row => row.State, AlarmState.Cleared)
                .SetProperty(row => row.ClearedAt, now)
                .SetProperty(row => row.ClearedBy, "System restart"));
    }

    private static async Task<int> HighestAsync(IQueryable<string> ids, int floor)
    {
        var values = await ids.ToListAsync();
        return Math.Max(floor, values.Select(NumberIn).DefaultIfEmpty(0).Max());
    }

    private static int NumberIn(string id) =>
        int.TryParse(id.AsSpan(id.LastIndexOf('-') + 1), out var number) ? number : 0;
}
