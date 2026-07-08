using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MediaMusic.Audio;

namespace MediaMusic.Platform;

/// <summary>
/// System tray integration (PRD §3.2). Keeps a tray icon while the main window
/// is minimized/closed and exposes a right-click context menu.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly PlayerService _playerService;
    private readonly ILogger<TrayIconService> _logger;
    
    // We use dynamic/object type here to prevent compile issues on non-Windows SDK targets
    // while keeping full Windows Forms functionality at runtime on Windows targets.
    private object? _notifyIconObj; 
    private bool _disposed;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIconService"/> class.
    /// </summary>
    public TrayIconService(PlayerService playerService, ILogger<TrayIconService> logger)
    {
        _playerService = playerService;
        _logger = logger;
    }

    /// <summary>Creates and displays the system tray icon.</summary>
    public void Show()
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("System tray icon is only supported on Windows.");
            return;
        }

        if (_notifyIconObj != null)
            return;

        try
        {
            _logger.LogInformation("Initializing system tray icon.");
            var notifyIcon = new System.Windows.Forms.NotifyIcon();
            
            var iconPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "favicon.ico");
            if (File.Exists(iconPath))
            {
                notifyIcon.Icon = new System.Drawing.Icon(iconPath);
            }
            else
            {
                notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            notifyIcon.Text = "MediaMusic";
            notifyIcon.Visible = true;

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add("Play/Pause", null, OnPlayPauseClick);
            contextMenu.Items.Add("Next Track", null, OnNextClick);
            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            contextMenu.Items.Add("Show Window", null, OnShowWindowClick);
            contextMenu.Items.Add("Settings", null, OnShowWindowClick); // Restore window to show settings
            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, OnExitClick);

            notifyIcon.ContextMenuStrip = contextMenu;
            notifyIcon.DoubleClick += OnTrayDoubleClick;

            _notifyIconObj = notifyIcon;
            _logger.LogInformation("System tray icon successfully created.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create system tray icon.");
            _notifyIconObj = null;
        }
    }

    /// <summary>Hides and disposes the system tray icon.</summary>
    public void Hide()
    {
        if (_notifyIconObj is System.Windows.Forms.NotifyIcon notifyIcon)
        {
            _logger.LogInformation("Disposing system tray icon.");
            try
            {
                notifyIcon.Visible = false;
                if (notifyIcon.ContextMenuStrip != null)
                {
                    notifyIcon.ContextMenuStrip.Dispose();
                }
                notifyIcon.DoubleClick -= OnTrayDoubleClick;
                notifyIcon.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while disposing system tray icon.");
            }
            finally
            {
                _notifyIconObj = null;
            }
        }
    }

    private void OnPlayPauseClick(object? sender, EventArgs e)
    {
        try
        {
            if (_playerService.State.IsPlaying)
                _playerService.Pause();
            else
                _playerService.Resume();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in tray Play/Pause click handler.");
        }
    }

    private void OnNextClick(object? sender, EventArgs e)
    {
        try
        {
            _playerService.Next();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in tray Next click handler.");
        }
    }

    private void OnShowWindowClick(object? sender, EventArgs e)
    {
        RestoreMainWindow();
    }

    private void OnTrayDoubleClick(object? sender, EventArgs e)
    {
        RestoreMainWindow();
    }

    private void OnExitClick(object? sender, EventArgs e)
    {
        _logger.LogInformation("Exit application requested from system tray.");
        try
        {
            Hide();
            WindowHelper.MainWindow?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during application exit from tray.");
        }
    }

    private void RestoreMainWindow()
    {
        try
        {
            if (WindowHelper.MainWindow != null)
            {
                WindowHelper.MainWindow.Minimized = false;
                var handle = WindowHelper.MainWindow.WindowHandle;
                if (handle != IntPtr.Zero)
                {
                    SetForegroundWindow(handle);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring main window from tray.");
        }
    }

    /// <summary>
    /// Disposes the service resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        Hide();
        _disposed = true;
    }
}
