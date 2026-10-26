using Ardan.Api.Data;
using Ardan.Core.Contracts;
using Ardan.Core.Engine;
using Microsoft.EntityFrameworkCore;

namespace Ardan.Api.Live;

/// <summary>Owns the live warehouse. Reads and commands share one lock, so nobody sees half a change.</summary>
public sealed class StoreHost(IDbContextFactory<ArdanDb> dbFactory, DbJournal journal, ILogger<StoreHost> logger)
{
    private readonly Lock _gate = new();
    private Warehouse? _warehouse;

    public LiveSnapshots Snapshots { get; } = new();

    public async Task StartAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var warehouse = await StoreLoader.LoadAsync(db, journal);
        _warehouse = warehouse;
        logger.LogInformation(
            "Store ready: {Stored} pallets in store ({Occupancy:P0}), {Orders} open orders.",
            warehouse.Grid.StoredCount,
            warehouse.Grid.Occupancy,
            warehouse.Orders.Count(order => order.IsOpen));
    }

    public T Read<T>(Func<Warehouse, T> read)
    {
        lock (_gate) return read(Warehouse);
    }

    public CommandResult Run(Func<Warehouse, CommandResult> command)
    {
        lock (_gate) return command(Warehouse);
    }

    public void Advance(TimeSpan elapsed)
    {
        lock (_gate) Warehouse.Advance(elapsed);
    }

    private Warehouse Warehouse => _warehouse ?? throw new InvalidOperationException("The store has not been loaded yet.");
}
