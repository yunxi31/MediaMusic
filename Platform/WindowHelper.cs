using Microsoft.JSInterop;
using MediaMusic.Platform.Win32;

namespace MediaMusic.Platform;

/// <summary>
/// Provides static JSInvokable endpoints for window state operations (minimize, maximize, close)
/// and window drag tracking for the chromeless window.
/// </summary>
public static class WindowHelper
{
    public static Photino.NET.PhotinoWindow? MainWindow { get; set; }

    [JSInvokable("MinimizeWindow")]
    public static void MinimizeWindow()
    {
        if (MainWindow != null)
        {
            MainWindow.Minimized = true;
        }
    }

    [JSInvokable("MaximizeWindow")]
    public static void MaximizeWindow()
    {
        if (MainWindow != null)
        {
            MainWindow.Maximized = !MainWindow.Maximized;
        }
    }

    [JSInvokable("CloseWindow")]
    public static void CloseWindow()
    {
        MainWindow?.Close();
    }

    [JSInvokable("StartDragWindow")]
    public static void StartDragWindow()
    {
        if (MainWindow != null)
        {
            Win32Interop.ReleaseCapture();
            Win32Interop.SendMessage(MainWindow.WindowHandle, Win32Interop.WM_NCLBUTTONDOWN, Win32Interop.HTCAPTION, 0);
        }
    }
}
