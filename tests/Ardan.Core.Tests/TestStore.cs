using Ardan.Core.Engine;
using Ardan.Core.Layout;
using Ardan.Core.Stock;

namespace Ardan.Core.Tests;

internal static class TestStore
{
    public static readonly DateTimeOffset Start = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);

    public static readonly SiteLayout Small = new(
        Levels: 3,
        Bays: 6,
        LaneDepth: 4,
        BayPitchMetres: 1.4,
        LevelHeightMetres: 1.9,
        PalletPitchMetres: 1.25);

    public static Warehouse Create(SiteLayout? layout = null, int taxis = 3) =>
        new(layout ?? Small, DemoSite.Skus, Start, new Random(7), NullJournal.Instance, taxis: taxis)
        {
            OrderFeedEnabled = false,
            FaultsEnabled = false,
            ProfileFaultRate = 0,
        };

    public static Pallet Pallet(string id, string sku = "GF-1001", string batch = "B1", int expiryDays = 20) => new()
    {
        Id = id,
        SkuCode = sku,
        Batch = batch,
        Expiry = DateOnly.FromDateTime(Start.AddDays(expiryDays).Date),
        WeightKg = 800,
        HeightMm = 1400,
    };

    public static void Put(Warehouse warehouse, int level, int bay, params Pallet[] pallets)
    {
        var lane = new LaneId(level, bay);
        var slot = warehouse.Grid[lane].Count;
        foreach (var pallet in pallets)
        {
            pallet.Lane = lane;
            pallet.Slot = slot++;
        }
        warehouse.Load(pallets);
    }

    public static void Run(Warehouse warehouse, TimeSpan time, Func<bool>? until = null)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < time; elapsed += TimeSpan.FromSeconds(1))
        {
            warehouse.Advance(TimeSpan.FromSeconds(1));
            if (until?.Invoke() == true) return;
        }
    }
}
