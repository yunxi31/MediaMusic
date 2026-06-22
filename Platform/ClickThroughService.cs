using MediaMusic.Platform.Win32;

namespace MediaMusic.Platform;

/// <summary>
/// Makes the Desktop Lyrics overlay window transparent to mouse input (PRD §2.3).
/// Photino.NET's SetTransparent(true) handles visual transparency; this service
/// adds the WS_EX_TRANSPARENT | WS_EX_LAYERED extended style so pointer events
/// pass through to whatever is behind the lyrics window.
/// </summary>
public sealed class ClickThroughService
{
    private readonly ILogger<ClickThroughService> _logger;

    public ClickThroughService(ILogger<ClickThroughService> logger) => _logger = logger;

    /// <summary>Enables click-through on the given window handle.</summary>
    public void Enable(IntPtr hwnd)
    {
        // TODO: var ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        //       SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        _logger.LogDebug("Enable click-through on {Hwnd} (stub).", hwnd);
        var ex = Win32Interop.GetWindowLong(hwnd, Win32Interop.GWL_EXSTYLE);
        Win32Interop.SetWindowLong(hwnd, Win32Interop.GWL_EXSTYLE,
            ex | Win32Interop.WS_EX_LAYERED | Win32Interop.WS_EX_TRANSPARENT);
    }

    /// <summary>Disables click-through so the lyrics window can be moved/closed.</summary>
    public void Disable(IntPtr hwnd)
    {
        var ex = Win32Interop.GetWindowLong(hwnd, Win32Interop.GWL_EXSTYLE);
        Win32Interop.SetWindowLong(hwnd, Win32Interop.GWL_EXSTYLE,
            ex & ~(Win32Interop.WS_EX_TRANSPARENT));
    }
}
