using Ardan.Core.Contracts;

namespace Ardan.Web.Services;

/// <summary>A short message shown in the corner after a command.</summary>
public sealed record Toast(int Id, string Text, string Tone);

/// <summary>Shell state for one browser tab: the help panel, search and toasts.</summary>
public sealed class UiState
{
    private static readonly TimeSpan ToastTime = TimeSpan.FromSeconds(5);
    private readonly List<Toast> _toasts = [];
    private int _nextToast;

    public bool HelpOpen { get; private set; }
    public bool SearchOpen { get; private set; }
    public IReadOnlyList<Toast> Toasts
    {
        get
        {
            lock (_toasts) return _toasts.ToList();
        }
    }

    public event Action? Changed;

    public void ToggleHelp()
    {
        HelpOpen = !HelpOpen;
        SearchOpen = false;
        Changed?.Invoke();
    }

    public void OpenSearch()
    {
        SearchOpen = true;
        HelpOpen = false;
        Changed?.Invoke();
    }

    public void CloseOverlays()
    {
        if (!HelpOpen && !SearchOpen) return;
        HelpOpen = false;
        SearchOpen = false;
        Changed?.Invoke();
    }

    public void Show(CommandResult result) => Show(result.Message, result.Ok ? "ok" : "crit");

    public void Show(string text, string tone = "ok")
    {
        var toast = new Toast(++_nextToast, text, tone);
        lock (_toasts) _toasts.Add(toast);
        Changed?.Invoke();
        _ = DismissLaterAsync(toast);
    }

    public void Dismiss(Toast toast)
    {
        lock (_toasts)
        {
            if (!_toasts.Remove(toast)) return;
        }
        Changed?.Invoke();
    }

    private async Task DismissLaterAsync(Toast toast)
    {
        await Task.Delay(ToastTime);
        Dismiss(toast);
    }
}
