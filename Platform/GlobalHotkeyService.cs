namespace MediaMusic.Platform;

/// <summary>
/// System-wide keyboard shortcuts (PRD §3.2). Registers global hotkeys so the
/// player can be controlled even when the window loses focus or is covered by
/// a fullscreen app. Implemented via Win32 RegisterHotKey/UnregisterHotKey.
/// </summary>
public sealed class GlobalHotkeyService
{
    private readonly ILogger<GlobalHotkeyService> _logger;

    public GlobalHotkeyService(ILogger<GlobalHotkeyService> logger) => _logger = logger;

    /// <summary>Registers the default media hotkeys (play/pause, prev, next, vol+/-).</summary>
    public void RegisterDefaults()
    {
        // TODO: hook a window's WndProc to receive WM_HOTKEY, then:
        //   RegisterHotKey(hwnd, id, modifiers, vk) for each binding.
        //   Map ids back to PlayerService actions.
        _logger.LogInformation("RegisterDefaults requested (stub).");
    }

    public void UnregisterAll()
    {
        // TODO: UnregisterHotKey for every registered id.
    }
}
