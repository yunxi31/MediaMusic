using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MediaMusic.Platform.Win32;

namespace MediaMusic.Platform;

/// <summary>
/// Makes the Desktop Lyrics overlay window transparent to mouse input (PRD §2.3).
/// Adds the WS_EX_TRANSPARENT | WS_EX_LAYERED extended style so pointer events
/// pass through to whatever is behind the lyrics window.
/// </summary>
public sealed class ClickThroughService
{
    private readonly ILogger<ClickThroughService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClickThroughService"/> class.
    /// </summary>
    public ClickThroughService(ILogger<ClickThroughService> logger) => _logger = logger;

    /// <summary>Enables click-through on the given window handle.</summary>
    public void Enable(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            _logger.LogWarning("Invalid window handle for Enable click-through.");
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("Click-through is only supported on Windows.");
            return;
        }

        try
        {
            int ex = Win32Interop.GetWindowLong(hwnd, Win32Interop.GWL_EXSTYLE);
            if (ex == 0)
            {
                int error = Marshal.GetLastWin32Error();
                _logger.LogWarning("GetWindowLong returned 0 for handle {Handle}. Win32 Error: {Error}", hwnd, error);
            }

            int result = Win32Interop.SetWindowLong(hwnd, Win32Interop.GWL_EXSTYLE,
                ex | Win32Interop.WS_EX_LAYERED | Win32Interop.WS_EX_TRANSPARENT);

            if (result == 0)
            {
                int error = Marshal.GetLastWin32Error();
                _logger.LogWarning("SetWindowLong returned 0 for handle {Handle}. Win32 Error: {Error}", hwnd, error);
            }
            else
            {
                _logger.LogDebug("Enabled click-through on handle {Hwnd}.", hwnd);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable click-through on window handle {Handle}", hwnd);
        }
    }

    /// <summary>Disables click-through so the lyrics window can be moved/closed.</summary>
    public void Disable(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            _logger.LogWarning("Invalid window handle for Disable click-through.");
            return;
        }

        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            int ex = Win32Interop.GetWindowLong(hwnd, Win32Interop.GWL_EXSTYLE);
            if (ex == 0)
            {
                int error = Marshal.GetLastWin32Error();
                _logger.LogWarning("GetWindowLong returned 0 for handle {Handle} in Disable. Win32 Error: {Error}", hwnd, error);
            }

            int result = Win32Interop.SetWindowLong(hwnd, Win32Interop.GWL_EXSTYLE,
                ex & ~(Win32Interop.WS_EX_TRANSPARENT));

            if (result == 0)
            {
                int error = Marshal.GetLastWin32Error();
                _logger.LogWarning("SetWindowLong returned 0 for handle {Handle} in Disable. Win32 Error: {Error}", hwnd, error);
            }
            else
            {
                _logger.LogDebug("Disabled click-through on handle {Hwnd}.", hwnd);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable click-through on window handle {Handle}", hwnd);
        }
    }
}
