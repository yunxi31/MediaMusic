namespace MediaMusic.Platform;

/// <summary>
/// System tray integration (PRD §3.2). Keeps a tray icon while the main window
/// is minimized/closed (per user preference) and exposes a right-click context
/// menu (quick play/pause, show/hide, exit). Implementation route (Win32
/// Shell_NotifyIcon P/Invoke vs H.NotifyIcon NuGet) is deferred to implementation.
/// </summary>
public sealed class TrayIconService
{
    private readonly ILogger<TrayIconService> _logger;

    public TrayIconService(ILogger<TrayIconService> logger) => _logger = logger;

    public void Show()
    {
        // TODO: create NotifyIcon with the app icon + context menu, wire menu items to services.
        _logger.LogInformation("TrayIcon Show (stub).");
    }

    public void Hide()
    {
        // TODO: dispose the NotifyIcon.
    }
}
