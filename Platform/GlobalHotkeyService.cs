using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MediaMusic.Platform;

/// <summary>
/// System-wide keyboard shortcuts (PRD §3.2). Uses a WH_KEYBOARD_LL low-level
/// keyboard hook so hotkeys work even when the window loses focus or is minimised.
/// Bindings are stored as human-readable strings such as "Ctrl + Shift + P" and
/// converted to virtual key codes at registration time.
/// </summary>
public sealed partial class GlobalHotkeyService : IDisposable
{
    // ── Win32 P/Invoke ────────────────────────────────────────────────────────

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN     = 0x0100;
    private const int WM_SYSKEYDOWN  = 0x0104;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
                                                   IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
                                                 IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly ILogger<GlobalHotkeyService> _logger;

    // Key: action id (e.g. "play_pause"), Value: parsed binding
    private readonly Dictionary<string, Binding> _bindings = new();

    // Key: action id, Value: callback to invoke
    private readonly Dictionary<string, Action> _handlers = new();

    private IntPtr _hookHandle;
    private LowLevelKeyboardProc? _hookProc; // keep alive to prevent GC
    private bool _enabled;

    public GlobalHotkeyService(ILogger<GlobalHotkeyService> logger) => _logger = logger;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Enables or disables global hotkey interception.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        if (_enabled == enabled) return;
        _enabled = enabled;

        if (enabled)
            Install();
        else
            Uninstall();
    }

    /// <summary>
    /// Registers an action callback for a given action id using the binding
    /// string from SettingsService (e.g. "Ctrl + Right").
    /// Call this again with a new <paramref name="bindingString"/> to remap.
    /// </summary>
    public void Register(string actionId, string bindingString, Action handler)
    {
        if (!TryParseBinding(bindingString, out var binding))
        {
            _logger.LogWarning("Could not parse binding '{Binding}' for action '{Action}'.",
                               bindingString, actionId);
            return;
        }

        _bindings[actionId]  = binding;
        _handlers[actionId]  = handler;

        _logger.LogDebug("Registered hotkey [{Binding}] → {Action}.", bindingString, actionId);
    }

    /// <summary>Removes a registered action.</summary>
    public void Unregister(string actionId)
    {
        _bindings.Remove(actionId);
        _handlers.Remove(actionId);
    }

    /// <summary>Removes all registered actions and uninstalls the hook.</summary>
    public void UnregisterAll()
    {
        _bindings.Clear();
        _handlers.Clear();
        Uninstall();
        _logger.LogInformation("All hotkeys unregistered.");
    }

    // ── Hook lifecycle ────────────────────────────────────────────────────────

    private void Install()
    {
        if (_hookHandle != IntPtr.Zero) return;

        _hookProc   = HookCallback;
        var hModule = GetModuleHandle(null);
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, hModule, 0);

        if (_hookHandle == IntPtr.Zero)
            _logger.LogError("SetWindowsHookEx failed (error {Error}).",
                             Marshal.GetLastWin32Error());
        else
            _logger.LogInformation("Low-level keyboard hook installed.");
    }

    private void Uninstall()
    {
        if (_hookHandle == IntPtr.Zero) return;

        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
        _hookProc   = null;
        _logger.LogInformation("Low-level keyboard hook removed.");
    }

    // ── Hook callback ─────────────────────────────────────────────────────────

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
        {
            var kbd    = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var vk     = (int)kbd.vkCode;
            bool ctrl  = (GetKeyState(0x11) & 0x8000) != 0; // VK_CONTROL
            bool alt   = (GetKeyState(0x12) & 0x8000) != 0; // VK_MENU
            bool shift = (GetKeyState(0x10) & 0x8000) != 0; // VK_SHIFT

            foreach (var (actionId, binding) in _bindings)
            {
                if (binding.VkCode == vk
                    && binding.Ctrl  == ctrl
                    && binding.Alt   == alt
                    && binding.Shift == shift)
                {
                    _logger.LogDebug("Hotkey fired → {Action}.", actionId);
                    if (_handlers.TryGetValue(actionId, out var handler))
                        handler();
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int vKey);

    // ── Binding parsing ───────────────────────────────────────────────────────

    private record struct Binding(int VkCode, bool Ctrl, bool Alt, bool Shift);

    private static bool TryParseBinding(string raw, out Binding binding)
    {
        binding = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var parts = raw.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        bool ctrl = false, alt = false, shift = false;
        string? mainKey = null;

        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":    ctrl  = true; break;
                case "alt":     alt   = true; break;
                case "shift":   shift = true; break;
                default:        mainKey = part; break;
            }
        }

        if (mainKey is null) return false;

        int vk = NameToVk(mainKey);
        if (vk == 0) return false;

        binding = new Binding(vk, ctrl, alt, shift);
        return true;
    }

    private static int NameToVk(string name) => name.ToUpperInvariant() switch
    {
        // Letters A–Z
        var s when s.Length == 1 && s[0] >= 'A' && s[0] <= 'Z' => (int)s[0],

        // Digits
        var s when s.Length == 1 && s[0] >= '0' && s[0] <= '9' => (int)s[0],

        // Named keys
        "SPACE"         => 0x20,
        "ENTER"         => 0x0D,
        "ESCAPE" or "ESC" => 0x1B,
        "BACK" or "BACKSPACE" => 0x08,
        "TAB"           => 0x09,
        "DELETE" or "DEL" => 0x2E,
        "INSERT"        => 0x2D,
        "HOME"          => 0x24,
        "END"           => 0x23,
        "PAGEUP"        => 0x21,
        "PAGEDOWN"      => 0x22,
        "LEFT"          => 0x25,
        "UP"            => 0x26,
        "RIGHT"         => 0x27,
        "DOWN"          => 0x28,
        "F1"            => 0x70,
        "F2"            => 0x71,
        "F3"            => 0x72,
        "F4"            => 0x73,
        "F5"            => 0x74,
        "F6"            => 0x75,
        "F7"            => 0x76,
        "F8"            => 0x77,
        "F9"            => 0x78,
        "F10"           => 0x79,
        "F11"           => 0x7A,
        "F12"           => 0x7B,
        "MEDIANEXT"     => 0xB0,
        "MEDIAPREV"     => 0xB1,
        "MEDIASTOP"     => 0xB2,
        "MEDIAPLAY"     => 0xB3,
        "VOLUMEMUTE"    => 0xAD,
        "VOLUMEDOWN"    => 0xAE,
        "VOLUMEUP"      => 0xAF,
        _               => 0,
    };

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose() => Uninstall();
}
