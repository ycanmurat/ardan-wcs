using System.Threading.Channels;
using Ardan.Core.Alarms;
using Ardan.Core.Engine;
using Ardan.Core.Orders;
using Ardan.Core.Stock;
using Microsoft.EntityFrameworkCore;

namespace Ardan.Api.Data;

/// <summary>Copies each change out of the live model at once, so the simulation never waits for the database.</summary>
public sealed class DbJournal : IJournal
{
    private readonly Channel<object> _changes = Channel.CreateUnbounded<object>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<object> Changes => _changes.Reader;

    public void PalletChanged(Pallet pallet) => _changes.Writer.TryWrite(pallet.ToRow());

    public void OrderChanged(Order order) => _changes.Writer.TryWrite(order.ToRow());

    public void Moved(Movement movement) => _changes.Writer.TryWrite(movement.ToRow());

    public void AlarmChanged(Alarm alarm) => _changes.Writer.TryWrite(alarm.ToRow());
}

/// <summary>Writes journal changes to SQLite in small batches.</summary>
public sealed class JournalWriter(DbJournal journal, IDbContextFactory<ArdanDb> dbFactory, ILogger<JournalWriter> logger)
    : BackgroundService
{
    private static readonly TimeSpan BatchWindow = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await journal.Changes.WaitToReadAsync(stoppingToken))
            {
                await Task.Delay(BatchWindow, stoppingToken);
                await FlushAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }

        await FlushAsync();
    }

    private async Task FlushAsync()
    {
        if (!journal.Changes.TryPeek(out _)) return;

        await using var db = await dbFactory.CreateDbContextAsync();
        while (journal.Changes.TryRead(out var change)) Apply(db, change);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "A batch of store changes could not be saved.");
        }
    }

    private static void Apply(ArdanDb db, object change)
    {
        switch (change)
        {
            case PalletRow row:
                Upsert(db.Pallets, row, row.Id);
                break;
            case OrderRow row:
                Upsert(db.Orders, row, row.Id);
                break;
            case AlarmRow row:
                Upsert(db.Alarms, row, row.Id);
                break;
            case MovementRow row:
                db.Movements.Add(row);
                break;
        }
    }

    private static void Upsert<T>(DbSet<T> set, T row, string key) where T : class
    {
        var existing = set.Find(key);
        if (existing is null) set.Add(row);
        else set.Entry(existing).CurrentValues.SetValues(row);
    }
}
