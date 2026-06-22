namespace MediaMusic.Platform;

/// <summary>
/// Creates and manages the auxiliary windows (PRD §3.1): the Mini Player and the
/// Desktop Lyrics overlay. Each window is its own <c>PhotinoBlazorApp</c> instance
/// with a dedicated root component and HTML entry (see official MultiWindowSample).
/// All windows share the same DI container, so singletons (PlayerService, AppState)
/// keep their state in sync across windows for free.
/// </summary>
public sealed class WindowManager
{
    private readonly ILogger<WindowManager> _logger;

    public WindowManager(ILogger<WindowManager> logger) => _logger = logger;

    /// <summary>Opens the Mini Player floating window.</summary>
    public void ShowMiniPlayer()
    {
        // TODO: build a second PhotinoBlazorApp with RootComponents.Add<MiniPlayer>("app"),
        //       load wwwroot/miniplayer.html, SetChromeless(true), SetSize(320,120), SetTopMost(true).
        _logger.LogInformation("ShowMiniPlayer requested (stub).");
    }

    /// <summary>Opens the always-on-top, transparent, click-through Desktop Lyrics window.</summary>
    public void ShowDesktopLyrics()
    {
        // TODO: PhotinoBlazorApp with DesktopLyrics root + wwwroot/lyrics.html,
        //       SetChromeless(true), SetTransparent(true), SetTopMost(true),
        //       then apply WS_EX_TRANSPARENT|LAYERED via ClickThroughService.
        _logger.LogInformation("ShowDesktopLyrics requested (stub).");
    }

    public void CloseMiniPlayer() { /* TODO */ }
    public void CloseDesktopLyrics() { /* TODO */ }
}
