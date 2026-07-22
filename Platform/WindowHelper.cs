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

    public static Audio.PlayerService? PlayerService { get; set; }

    private static readonly Dictionary<string, string> ActionToCombination = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> CombinationToAction = new(StringComparer.OrdinalIgnoreCase);

    public static void RegisterShortcut(string actionId, string combination)
    {
        if (string.IsNullOrWhiteSpace(actionId) || string.IsNullOrWhiteSpace(combination)) return;
        
        if (ActionToCombination.TryGetValue(actionId, out var oldComb))
        {
            CombinationToAction.Remove(oldComb);
        }
        ActionToCombination[actionId] = combination;
        CombinationToAction[combination] = actionId;
    }

    [JSInvokable("HandleGlobalShortcut")]
    public static bool HandleGlobalShortcut(string combination)
    {
        if (PlayerService == null || string.IsNullOrWhiteSpace(combination)) return false;

        if (!CombinationToAction.TryGetValue(combination, out var actionId)) return false;

        switch (actionId)
        {
            case "play_pause":
                PlayerService.TogglePlayPause();
                return true;
            case "next":
                PlayerService.Next();
                return true;
            case "prev":
                PlayerService.Previous();
                return true;
            case "vol_up":
                PlayerService.SetVolume(Math.Min(1.0, PlayerService.State.Volume + 0.05));
                return true;
            case "vol_down":
                PlayerService.SetVolume(Math.Max(0.0, PlayerService.State.Volume - 0.05));
                return true;
            default:
                return false;
        }
    }

    [JSInvokable("TogglePlayPause")]
    public static void TogglePlayPause()
    {
        PlayerService?.TogglePlayPause();
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
