using MediaMusic.Platform.Win32;

namespace MediaMusic.Platform;

/// <summary>
/// Drags the chromeless window when the user mouse-drags the custom title bar
/// (PRD §3.1). Photino.NET has no built-in window-drag API, so the title bar
/// raises a JS mousedown that calls back into C# which releases the mouse
/// capture and posts WM_NCLBUTTONDOWN(HTCAPTION) to the window.
/// </summary>
public sealed class WindowDragService
{
    private readonly ILogger<WindowDragService> _logger;

    public WindowDragService(ILogger<WindowDragService> logger) => _logger = logger;

    /// <summary>Called from a [JSInvokable] handler bound to the title bar mousedown.</summary>
    public void StartDrag(IntPtr hwnd)
    {
        // TODO: Win32Interop.ReleaseCapture(); Win32Interop.SendMessage(hwnd, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        _logger.LogDebug("StartDrag on {Hwnd} (stub).", hwnd);
        Win32Interop.ReleaseCapture();
        Win32Interop.SendMessage(hwnd, Win32Interop.WM_NCLBUTTONDOWN, Win32Interop.HTCAPTION, 0);
    }
}
