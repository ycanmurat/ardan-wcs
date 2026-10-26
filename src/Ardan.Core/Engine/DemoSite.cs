using Ardan.Core.Layout;
using Ardan.Core.Stock;

namespace Ardan.Core.Engine;

/// <summary>Stock and history for a fresh demo store.</summary>
public sealed record StockPlan(IReadOnlyList<Pallet> Pallets, IReadOnlyList<Movement> Movements, int LastPalletNumber);

/// <summary>A demo site: a chilled food distribution centre. The company and its products are made up.</summary>
public static class DemoSite
{
    public const string Company = "Glenmore Foods";
    public const string Site = "Chilled DC · Dundalk";

    public static IReadOnlyList<Sku> Skus { get; } =
    [
        new("GF-1001", "Mature cheddar, 5 kg block", "Dairy", Velocity.A, 80, 60),
        new("GF-1002", "Salted butter, 40 × 454 g", "Dairy", Velocity.A, 60, 45),
        new("GF-1003", "Whole milk, 6 × 2 L", "Dairy", Velocity.A, 72, 10),
        new("GF-1004", "Free-range eggs, 30 × 6", "Eggs", Velocity.A, 40, 21),
        new("GF-1005", "Back rashers, 2.5 kg", "Meat", Velocity.A, 64, 14),
        new("GF-1006", "Natural yoghurt, 12 × 500 g", "Dairy", Velocity.A, 90, 21),
        new("GF-2001", "Smoked salmon sides, 1 kg", "Fish", Velocity.B, 50, 18),
        new("GF-2002", "Black pudding, 2.27 kg", "Meat", Velocity.B, 56, 30),
        new("GF-2003", "Buttermilk, 12 × 1 L", "Dairy", Velocity.B, 60, 14),
        new("GF-2004", "Soft cheese, 12 × 150 g", "Dairy", Velocity.B, 88, 35),
        new("GF-2005", "Fresh soup, 6 × 600 ml", "Ready meals", Velocity.B, 70, 12),
        new("GF-2006", "Chicken fillets, 2 kg", "Poultry", Velocity.B, 48, 9),
        new("GF-2007", "Apple juice, 12 × 1 L", "Drinks", Velocity.B, 75, 40),
        new("GF-2008", "Coleslaw, 6 × 1 kg", "Deli", Velocity.B, 70, 12),
        new("GF-3001", "Goat's cheese log, 1 kg", "Dairy", Velocity.C, 60, 40),
        new("GF-3002", "Crème fraîche, 2 L", "Dairy", Velocity.C, 55, 28),
        new("GF-3003", "Fish pie, 4 × 1.2 kg", "Ready meals", Velocity.C, 40, 10),
        new("GF-3004", "Lamb shanks, 2 × 400 g", "Meat", Velocity.C, 45, 14),
        new("GF-3005", "Oat drink, 12 × 1 L", "Drinks", Velocity.C, 75, 60),
        new("GF-3006", "Hummus, 4 × 1 kg", "Deli", Velocity.C, 60, 21),
    ];

    public static StockPlan Stock(SiteLayout layout, DateTimeOffset now, Random random, double fill = 0.68)
    {
        var grid = new StorageGrid(layout);
        var pallets = new List<Pallet>();
        var movements = new List<Movement>();
        var number = 100_000;

        var keepEmpty = (int)(layout.LaneCount * 0.2);
        for (var attempt = 0; attempt < 2_000 && grid.Occupancy < fill; attempt++)
        {
            var sku = Skus[random.Next(Skus.Count)];
            if (sku.Velocity == Velocity.C && random.NextDouble() < 0.5) continue;

            var ageDays = random.NextDouble() * Math.Min(10, sku.ShelfLifeDays - 3);
            var received = now.AddDays(-ageDays);
            var batch = $"B{received:yyMMdd}-{random.Next(10, 99)}";
            var expiry = DateOnly.FromDateTime(received.AddDays(sku.ShelfLifeDays).Date);

            if (grid.Lanes.Count(lane => lane.IsEmpty) <= keepEmpty) break;

            for (var i = 0; i < layout.LaneDepth - random.Next(3); i++)
            {
                var pallet = new Pallet
                {
                    Id = $"PLT-{++number}",
                    SkuCode = sku.Code,
                    Batch = batch,
                    Expiry = expiry,
                    WeightKg = 380 + random.Next(750),
                    HeightMm = 1050 + random.Next(600),
                    ReceivedAt = received,
                };

                var lane = Slotting.LaneFor(grid, pallet, sku.Velocity);
                if (lane is null) break;
                lane.Push(pallet);
                pallets.Add(pallet);

                var taxi = $"T-{1 + random.Next(6):00}";
                var storedAt = received.AddMinutes(2 + random.NextDouble() * 4);
                movements.Add(new Movement(pallet.Id, received, MovementKind.Received, "Dock", "Infeed", "Profile check"));
                movements.Add(new Movement(pallet.Id, storedAt, MovementKind.Stored, taxi, lane.Id.ToString(), taxi));
            }
        }

        return new StockPlan(pallets, movements, number);
    }
}
