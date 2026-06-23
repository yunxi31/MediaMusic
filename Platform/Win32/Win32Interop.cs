namespace MediaMusic.Platform.Win32;

/// <summary>
/// Centralized Win32 P/Invoke declarations used by the platform services
/// (window drag, click-through, global hotkeys, tray). Kept in one place so
/// the rest of <c>Platform/</c> stays readable.
/// </summary>
internal static class Win32Interop
{
    // --- Window drag (chromeless custom title bar) ---
    public const int WM_NCLBUTTONDOWN = 0xA1;
    public const int HTCAPTION = 2;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern int ReleaseCapture();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    // --- Click-through (desktop lyrics overlay) ---
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    // --- Global hotkeys ---
    public const int WM_HOTKEY = 0x0312;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // --- Window style (restore minimize box on chromeless windows) ---
    public const int GWL_STYLE = -16;
    public const uint WS_MINIMIZEBOX = 0x00020000;
    public const uint WS_MAXIMIZEBOX = 0x00010000;

    // x64 requires SetWindowLongPtr; alias via entry point name trick.
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    /// <summary>Gets the window style bits (GWL_STYLE) in a pointer-safe way.</summary>
    public static uint GetStyle(IntPtr hWnd)
        => (uint)GetWindowLongPtr64(hWnd, GWL_STYLE).ToInt64();

    /// <summary>Sets the window style bits (GWL_STYLE) in a pointer-safe way.</summary>
    public static void SetStyle(IntPtr hWnd, uint newStyle)
        => SetWindowLongPtr64(hWnd, GWL_STYLE, new IntPtr((long)newStyle));

    // TODO: add NotifyIcon / Shell_NotifyIcon declarations for the system tray,
    //       or switch to the H.NotifyIcon NuGet (decide at implementation time).
}
