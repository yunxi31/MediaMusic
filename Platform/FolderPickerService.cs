using System.Runtime.InteropServices;
using Microsoft.JSInterop;

namespace MediaMusic.Platform;

/// <summary>
/// Native folder-picker dialog. Uses the classic Win32 <c>SHBrowseForFolder</c>
/// API — simple, reliable P/Invoke with no complex COM vtable layout to get wrong.
/// Exposed to Blazor JS via <c>DotNet.invokeMethodAsync('MediaMusic', 'PickFolder')</c>.
/// </summary>
public static class FolderPicker
{
    /// <summary>
    /// Shows a native folder-picker dialog and returns the chosen path, or
    /// <c>null</c> if the user cancelled.
    /// </summary>
    [JSInvokable("PickFolder")]
    public static string? PickFolder()
    {
        try
        {
            return ShowFolderDialog();
        }
        catch
        {
            return null;
        }
    }

    private static string? ShowFolderDialog()
    {
        var title = "选择音乐文件夹";

        var bi = new BROWSEINFO
        {
            hwndOwner = IntPtr.Zero,
            pidlRoot = IntPtr.Zero,
            pszDisplayName = IntPtr.Zero,
            lpszTitle = title,
            ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE,
            lpfn = IntPtr.Zero,
            lParam = IntPtr.Zero,
            iImage = 0
        };

        var pidl = SHBrowseForFolder(ref bi);
        if (pidl == IntPtr.Zero)
            return null; // user cancelled

        try
        {
            var pathBuffer = Marshal.AllocHGlobal(260 * 2); // Unicode MAX_PATH
            try
            {
                if (SHGetPathFromIDList(pidl, pathBuffer))
                    return Marshal.PtrToStringUni(pathBuffer)?.Trim();
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(pathBuffer);
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(pidl);
        }
    }

    // ── Win32 P/Invoke declarations ──

    private const uint BIF_RETURNONLYFSDIRS = 0x0001;
    private const uint BIF_NEWDIALOGSTYLE = 0x0040;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BROWSEINFO
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public IntPtr pszDisplayName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO lpbi);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SHGetPathFromIDList(IntPtr pidl, IntPtr pszPath);
}
