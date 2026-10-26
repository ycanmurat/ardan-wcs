using Ardan.Core.Alarms;
using Ardan.Core.Engine;
using Ardan.Core.Orders;
using Ardan.Core.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ardan.Api.Data;

/// <summary>The store database: products, pallets, orders, pallet history and alarms.</summary>
public sealed class ArdanDb(DbContextOptions<ArdanDb> options) : DbContext(options)
{
    public DbSet<SkuRow> Skus => Set<SkuRow>();
    public DbSet<PalletRow> Pallets => Set<PalletRow>();
    public DbSet<OrderRow> Orders => Set<OrderRow>();
    public DbSet<MovementRow> Movements => Set<MovementRow>();
    public DbSet<AlarmRow> Alarms => Set<AlarmRow>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        builder.Properties<Velocity>().HaveConversion<string>();
        builder.Properties<PalletStatus>().HaveConversion<string>();
        builder.Properties<OrderType>().HaveConversion<string>();
        builder.Properties<OrderStatus>().HaveConversion<string>();
        builder.Properties<Priority>().HaveConversion<string>();
        builder.Properties<MovementKind>().HaveConversion<string>();
        builder.Properties<Severity>().HaveConversion<string>();
        builder.Properties<AlarmState>().HaveConversion<string>();
        builder.Properties<AlarmAction>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<SkuRow>().HasKey(row => row.Code);
        model.Entity<PalletRow>().HasKey(row => row.Id);
        model.Entity<PalletRow>().HasIndex(row => row.Status);
        model.Entity<OrderRow>().HasKey(row => row.Id);
        model.Entity<OrderRow>().HasIndex(row => row.Status);
        model.Entity<MovementRow>().HasKey(row => row.Id);
        model.Entity<MovementRow>().HasIndex(row => row.PalletId);
        model.Entity<AlarmRow>().HasKey(row => row.Id);
        model.Entity<AlarmRow>().HasIndex(row => row.RaisedAt);
    }
}

public sealed class SkuRow
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public Velocity Velocity { get; set; }
    public int CasesPerPallet { get; set; }
    public int ShelfLifeDays { get; set; }
}

public sealed class PalletRow
{
    public string Id { get; set; } = "";
    public string SkuCode { get; set; } = "";
    public string Batch { get; set; } = "";
    public DateOnly Expiry { get; set; }
    public int WeightKg { get; set; }
    public int HeightMm { get; set; }
    public PalletStatus Status { get; set; }
    public int? Level { get; set; }
    public int? Bay { get; set; }
    public int Slot { get; set; }
    public string? OrderId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}

public sealed class OrderRow
{
    public string Id { get; set; } = "";
    public OrderType Type { get; set; }
    public string Reference { get; set; } = "";
    public string SkuCode { get; set; } = "";
    public int Quantity { get; set; }
    public int Dock { get; set; }
    public Priority Priority { get; set; }
    public OrderStatus Status { get; set; }
    public string Source { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Batch { get; set; }
    public DateOnly? Expiry { get; set; }
    public int Done { get; set; }
    public int Returned { get; set; }
    public string? Note { get; set; }
}

public sealed class MovementRow
{
    public long Id { get; set; }
    public string PalletId { get; set; } = "";
    public DateTimeOffset Time { get; set; }
    public MovementKind Kind { get; set; }
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string? By { get; set; }
}

public sealed class AlarmRow
{
    public string Id { get; set; } = "";
    public string Code { get; set; } = "";
    public Severity Severity { get; set; }
    public string Source { get; set; } = "";
    public string Title { get; set; } = "";
    public string What { get; set; } = "";
    public string Cause { get; set; } = "";
    public string Steps { get; set; } = "";
    public AlarmAction Action { get; set; }
    public string? ActionLabel { get; set; }
    public string? Subject { get; set; }
    public DateTimeOffset RaisedAt { get; set; }
    public AlarmState State { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? ClearedAt { get; set; }
    public string? ClearedBy { get; set; }
}
