using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Photino.Blazor;
using MediaMusic.Audio;
using MediaMusic.Lyrics;
using MediaMusic.Services;
using MediaMusic.Windows;

namespace MediaMusic.Platform;

/// <summary>
/// Creates and manages the auxiliary windows (PRD §3.1): the Mini Player and the
/// Desktop Lyrics overlay. Each window is its own <c>PhotinoBlazorApp</c> instance
/// running on its own STA thread with a shared DI container.
/// </summary>
public sealed class WindowManager : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ClickThroughService _clickThroughService;
    private readonly WindowDragService _windowDragService;
    private readonly ILogger<WindowManager> _logger;

    private PhotinoBlazorApp? _miniPlayerWindow;
    private PhotinoBlazorApp? _lyricsWindow;
    private IntPtr _miniPlayerHandle = IntPtr.Zero;
    private IntPtr _lyricsHandle = IntPtr.Zero;
    private readonly object _windowLock = new();
    private bool _disposed;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowManager"/> class.
    /// </summary>
    public WindowManager(
        IServiceProvider serviceProvider,
        ClickThroughService clickThroughService,
        WindowDragService windowDragService,
        ILogger<WindowManager> logger)
    {
        _serviceProvider = serviceProvider;
        _clickThroughService = clickThroughService;
        _windowDragService = windowDragService;
        _logger = logger;
    }

    /// <summary>Displays the Mini Player window (320x120 pixels, chromeless, top-most).</summary>
    public void ShowMiniPlayer()
    {
        lock (_windowLock)
        {
            if (_miniPlayerWindow != null)
            {
                _logger.LogInformation("MiniPlayer window is already open. Bringing it to front.");
                BringWindowToFront(_miniPlayerHandle);
                return;
            }

            _logger.LogInformation("Opening MiniPlayer window.");

            var thread = new Thread(() =>
            {
                try
                {
                    var builder = PhotinoBlazorAppBuilder.CreateDefault();
                    
                    // Copy singleton services from parent container
                    builder.Services.AddSingleton(_serviceProvider.GetRequiredService<PlayerService>());
                    builder.Services.AddSingleton(_serviceProvider.GetRequiredService<LyricsService>());
                    builder.Services.AddSingleton(_serviceProvider.GetRequiredService<ThemeService>());
                    builder.Services.AddSingleton(_serviceProvider.GetRequiredService<SettingsService>());
                    builder.Services.AddSingleton(_serviceProvider.GetRequiredService<AppState>());
                    builder.Services.AddSingleton(_clickThroughService);
                    builder.Services.AddSingleton(_windowDragService);
                    builder.Services.AddLogging();

                    builder.RootComponents.Add<MiniPlayer>("app");

                    var app = builder.Build();
                    
                    app.MainWindow
                        .SetChromeless(true)
                        .SetTopMost(true)
                        .SetSize(320, 120)
                        .SetTitle("MediaMusic Mini Player")
                        .SetUseOsDefaultSize(false)
                        .SetContextMenuEnabled(false);

                    // Load custom entry page
                    app.MainWindow.Load("wwwroot/miniplayer.html");

                    lock (_windowLock)
                    {
                        _miniPlayerWindow = app;
                        _miniPlayerHandle = app.MainWindow.WindowHandle;
                    }

                    app.Run();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception running MiniPlayer window loop.");
                }
                finally
                {
                    lock (_windowLock)
                    {
                        _miniPlayerWindow = null;
                        _miniPlayerHandle = IntPtr.Zero;
                    }
                    _logger.LogInformation("MiniPlayer window closed.");
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
    }

    /// <summary>Displays the Desktop Lyrics transparent topmost overlay window.</summary>
    public void ShowDesktopLyrics()
    {
        lock (_windowLock)
        {
            if (_lyricsWindow != null)
            {
                _logger.LogInformation("DesktopLyrics window is already open. Bringing it to front.");
                BringWindowToFront(_lyricsHandle);
                return;
            }

            _logger.LogInformation("Opening DesktopLyrics window.");

            var thread = new Thread(() =>
            {
                try
                {
                    var builder = PhotinoBlazorAppBuilder.CreateDefault();

                    // Copy singleton services from parent container
                    builder.Services.AddSingleton(_serviceProvider.GetRequiredService<PlayerService>());
                    builder.Services.AddSingleton(_serviceProvider.GetRequiredService<LyricsService>());
                    builder.Services.AddSingleton(_serviceProvider.GetRequiredService<ThemeService>());
                    builder.Services.AddSingleton(_serviceProvider.GetRequiredService<SettingsService>());
                    builder.Services.AddSingleton(_serviceProvider.GetRequiredService<AppState>());
                    builder.Services.AddSingleton(_clickThroughService);
                    builder.Services.AddSingleton(_windowDragService);
                    builder.Services.AddLogging();

                    builder.RootComponents.Add<DesktopLyrics>("app");

                    var app = builder.Build();

                    app.MainWindow
                        .SetChromeless(true)
                        .SetTransparent(true)
                        .SetTopMost(true)
                        .SetSize(800, 100)
                        .SetTitle("MediaMusic Desktop Lyrics")
                        .SetUseOsDefaultSize(false)
                        .SetContextMenuEnabled(false);

                    // Load custom transparent entry page
                    app.MainWindow.Load("wwwroot/lyrics.html");

                    // Register window created handler to apply click-through window styles
                    app.MainWindow.RegisterWindowCreatedHandler((sender, e) =>
                    {
                        var hwnd = app.MainWindow.WindowHandle;
                        if (hwnd != IntPtr.Zero)
                        {
                            _clickThroughService.Enable(hwnd);
                        }
                    });

                    lock (_windowLock)
                    {
                        _lyricsWindow = app;
                        _lyricsHandle = app.MainWindow.WindowHandle;
                    }

                    app.Run();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception running DesktopLyrics window loop.");
                }
                finally
                {
                    lock (_windowLock)
                    {
                        _lyricsWindow = null;
                        _lyricsHandle = IntPtr.Zero;
                    }
                    _logger.LogInformation("DesktopLyrics window closed.");
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
    }

    /// <summary>Closes the Mini Player window if it is currently open.</summary>
    public void CloseMiniPlayer()
    {
        lock (_windowLock)
        {
            if (_miniPlayerWindow != null)
            {
                _logger.LogInformation("Closing MiniPlayer window.");
                try
                {
                    _miniPlayerWindow.MainWindow.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while closing MiniPlayer window.");
                }
                finally
                {
                    _miniPlayerWindow = null;
                    _miniPlayerHandle = IntPtr.Zero;
                }
            }
        }
    }

    /// <summary>Closes the Desktop Lyrics overlay window if it is currently open.</summary>
    public void CloseDesktopLyrics()
    {
        lock (_windowLock)
        {
            if (_lyricsWindow != null)
            {
                _logger.LogInformation("Closing DesktopLyrics window.");
                try
                {
                    _lyricsWindow.MainWindow.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while closing DesktopLyrics window.");
                }
                finally
                {
                    _lyricsWindow = null;
                    _lyricsHandle = IntPtr.Zero;
                }
            }
        }
    }

    private void BringWindowToFront(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero && OperatingSystem.IsWindows())
        {
            try
            {
                ShowWindow(hwnd, SW_RESTORE);
                SetForegroundWindow(hwnd);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to bring window to front for handle {Handle}.", hwnd);
            }
        }
    }

    /// <summary>
    /// Disposes the manager resources, closing any open child windows.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        CloseMiniPlayer();
        CloseDesktopLyrics();
        _disposed = true;
    }
}
