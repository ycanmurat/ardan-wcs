using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;

namespace Ardan.Api.Live;

/// <summary>The SignalR hub screens connect to. The server only pushes; screens send commands over HTTP.</summary>
public sealed class LiveHub : Hub;

/// <summary>Moves the simulation forward ten times a second.</summary>
public sealed class SimulationLoop(StoreHost store) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan LongestStep = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Tick);
        var clock = Stopwatch.StartNew();
        var last = clock.Elapsed;

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = clock.Elapsed;
            var elapsed = now - last;
            last = now;
            store.Advance(elapsed < LongestStep ? elapsed : LongestStep);
        }
    }
}

/// <summary>Sends a live snapshot to every open screen four times a second.</summary>
public sealed class LiveBroadcaster(StoreHost store, IHubContext<LiveHub> hub, ILogger<LiveBroadcaster> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var snapshot = store.Read(store.Snapshots.Next);
            try
            {
                await hub.Clients.All.SendAsync("snapshot", snapshot, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "A live snapshot could not be sent.");
            }
        }
    }
}
