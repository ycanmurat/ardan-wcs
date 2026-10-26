using System.Collections.Concurrent;
using System.Text.Json;
using Ardan.Core.Contracts;
using Microsoft.AspNetCore.SignalR.Client;

namespace Ardan.Web.Services;

public enum FeedStatus { Connecting, Live, Reconnecting, Offline }

/// <summary>One live connection to the control server, shared by every open screen.</summary>
public sealed class LiveFeed(IHttpClientFactory httpFactory, ILogger<LiveFeed> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);
    private readonly ConcurrentDictionary<(int Level, int Bay), LaneView> _lanes = new();

    public LiveSnapshot? Current { get; private set; }
    public LayoutView? Layout { get; private set; }
    public FeedStatus Status { get; private set; } = FeedStatus.Connecting;
    public long LaneVersion { get; private set; }
    public IEnumerable<LaneView> Lanes => _lanes.Values;

    public event Action? Changed;

    public LaneView? LaneAt(int level, int bay) => _lanes.GetValueOrDefault((level, bay));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var api = Api();
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(api.BaseAddress!, "hubs/live"))
            .WithAutomaticReconnect(new RetryForever())
            .AddJsonProtocol(options => options.PayloadSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Build();

        hub.On<LiveSnapshot>("snapshot", Apply);
        hub.Reconnecting += _ =>
        {
            SetStatus(FeedStatus.Reconnecting);
            return Task.CompletedTask;
        };
        hub.Reconnected += async _ =>
        {
            await LoadLayoutAsync();
            SetStatus(FeedStatus.Live);
        };

        while (!stoppingToken.IsCancellationRequested && hub.State == HubConnectionState.Disconnected)
        {
            try
            {
                await hub.StartAsync(stoppingToken);
                await LoadLayoutAsync();
                SetStatus(FeedStatus.Live);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                SetStatus(FeedStatus.Offline);
                logger.LogInformation("Control server not reachable yet: {Reason}", exception.Message);
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }

        await hub.DisposeAsync();
    }

    private HttpClient Api() => httpFactory.CreateClient(nameof(WcsApi));

    private async Task LoadLayoutAsync()
    {
        var api = new WcsApi(Api());
        Layout = await api.LayoutAsync() ?? Layout;
        _lanes.Clear();
        Merge(await api.LanesAsync());
        LaneVersion++;
        Changed?.Invoke();
    }

    private void Apply(LiveSnapshot snapshot)
    {
        if (Current is not null && snapshot.Version < Current.Version) _lanes.Clear();
        if (Merge(snapshot.ChangedLanes)) LaneVersion++;
        Current = snapshot;
        Changed?.Invoke();
    }

    private bool Merge(IEnumerable<LaneView> lanes)
    {
        var changed = false;
        foreach (var lane in lanes)
        {
            var key = (lane.Level, lane.Bay);
            if (_lanes.TryGetValue(key, out var known) && known.Version >= lane.Version) continue;
            _lanes[key] = lane;
            changed = true;
        }
        return changed;
    }

    private void SetStatus(FeedStatus status)
    {
        if (Status == status) return;
        Status = status;
        Changed?.Invoke();
    }

    private sealed class RetryForever : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext context) => RetryDelay;
    }
}
