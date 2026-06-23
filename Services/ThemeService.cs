using Microsoft.JSInterop;

namespace MediaMusic.Services;

/// <summary>
/// Holds observable application-wide state (current track, play queue, play mode,
/// theme) so Blazor components across multiple windows stay in sync via the
/// singleton instance shared through DI.
/// </summary>
public sealed class AppState
{
    private readonly ILogger<AppState> _logger;

    public AppState(ILogger<AppState> logger) => _logger = logger;

    /// <summary>The currently active theme ("light" or "dark").</summary>
    public string Theme { get; private set; } = "dark";

    /// <summary>Whether the user has completed the first-run onboarding wizard.</summary>
    public bool IsOnboardingComplete { get; private set; }

    /// <summary>Whether the sidebar is collapsed to show only icons.</summary>
    public bool IsSidebarCollapsed { get; private set; }

    public void SetSidebarCollapsed(bool value)
    {
        if (IsSidebarCollapsed == value) return;
        IsSidebarCollapsed = value;
        OnChanged();
    }

    /// <summary>Root directories chosen during onboarding / settings.</summary>
    public List<string> ScanRoots { get; } = new();

    /// <summary>Live scan progress (null when idle).</summary>
    public ScanProgressState? ScanProgress { get; private set; }

    public void SetOnboardingComplete(bool value)
    {
        IsOnboardingComplete = value;
        OnChanged();
    }

    public void SetScanProgress(MediaMusic.Library.ScanProgress? progress)
    {
        ScanProgress = progress is null
            ? null
            : new ScanProgressState(progress.Processed, progress.TotalFound, progress.CurrentFile);
        OnChanged();
    }


    /// <summary>Raised whenever any state property changes.</summary>
    public event EventHandler? Changed;

    public void SetTheme(string theme)
    {
        if (Theme == theme)
            return;
        Theme = theme;
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>Snapshot of a running scan for UI binding.</summary>
public sealed record ScanProgressState(int Processed, int TotalFound, string CurrentFile);

/// <summary>
/// Applies the light/dark theme by toggling the <c>dark</c> class on
/// <c>&lt;html&gt;</c> via JS interop, and persists the user's preference.
/// </summary>
public sealed class ThemeService
{
    private readonly IJSRuntime _js;
    private readonly SettingsService _settings;
    private readonly AppState _state;
    private readonly ILogger<ThemeService> _logger;

    public ThemeService(IJSRuntime js, SettingsService settings, AppState state, ILogger<ThemeService> logger)
    {
        _js = js;
        _settings = settings;
        _state = state;
        _logger = logger;
    }

    /// <summary>Initial theme applied on startup.</summary>
    public const string DefaultTheme = "dark";

    /// <summary>Toggles between light and dark and persists the choice.</summary>
    public async Task ToggleAsync()
    {
        var next = _state.Theme == "dark" ? "light" : "dark";
        await ApplyAsync(next);
        await _settings.SetAsync("theme", next);
    }

    /// <summary>Applies the given theme to the DOM and updates <see cref="AppState"/>.</summary>
    public async Task ApplyAsync(string theme)
    {
        try
        {
            await _js.InvokeVoidAsync("mediamusic.theme.set", theme);
            _state.SetTheme(theme);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply theme {Theme}.", theme);
        }
    }
}
