using Ardan.Core.Contracts;
using Ardan.Web.Services;
using Microsoft.AspNetCore.Components;

namespace Ardan.Web.Components;

/// <summary>Base for screens that redraw when live data arrives, at most once per RefreshEvery.</summary>
public abstract class LiveComponent : ComponentBase, IDisposable
{
    private DateTime _lastDraw;
    private int _drawing;
    private bool _disposed;

    [Inject] protected LiveFeed Feed { get; set; } = default!;

    protected LiveSnapshot? Live => Feed.Current;

    protected DateTimeOffset Now => Feed.Current?.Time ?? DateTimeOffset.UtcNow;

    protected virtual TimeSpan RefreshEvery => TimeSpan.Zero;

    protected sealed override void OnInitialized() => Feed.Changed += OnFeedChanged;

    /// <summary>Runs before each live redraw. Screens use it to reload data that is not in the snapshot.</summary>
    protected virtual Task OnLiveAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _disposed = true;
        Feed.Changed -= OnFeedChanged;
        OnDispose();
        GC.SuppressFinalize(this);
    }

    protected virtual void OnDispose()
    {
    }

    private void OnFeedChanged()
    {
        var now = DateTime.UtcNow;
        if (_disposed || now - _lastDraw < RefreshEvery) return;
        if (Interlocked.Exchange(ref _drawing, 1) == 1) return;
        _lastDraw = now;
        _ = InvokeAsync(async () =>
        {
            try
            {
                if (_disposed) return;
                await OnLiveAsync();
                StateHasChanged();
            }
            finally
            {
                Volatile.Write(ref _drawing, 0);
            }
        });
    }
}
