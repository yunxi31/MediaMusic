using System;
using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowDragService"/> class.
    /// </summary>
    public WindowDragService(ILogger<WindowDragService> logger) => _logger = logger;

    /// <summary>Called from a [JSInvokable] handler bound to the title bar mousedown.</summary>
    /// <param name="hwnd">The window handle.</param>
    public void StartDrag(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            _logger.LogWarning("Invalid window handle for drag operation.");
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("Window drag P/Invoke is only supported on Windows.");
            return;
        }

        try
        {
            _logger.LogDebug("Initiating window drag on handle {Hwnd}.", hwnd);
            Win32Interop.ReleaseCapture();
            Win32Interop.SendMessage(hwnd, Win32Interop.WM_NCLBUTTONDOWN, Win32Interop.HTCAPTION, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate window drag on handle {Hwnd}", hwnd);
        }
    }
}
