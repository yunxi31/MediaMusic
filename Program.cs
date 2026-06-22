using MediaMusic.Services;
using Photino.Blazor;

namespace MediaMusic;

/// <summary>
/// Application entry point. Bootstraps the Photino.Blazor host, registers
/// all MediaMusic services, configures the chromeless main window, and starts
/// the native message loop.
/// </summary>
internal sealed class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);

        appBuilder.Services.AddLogging();

        // Register all MediaMusic application services (audio, data, library, platform, ...).
        ServiceRegistration.Register(appBuilder.Services);

        // Wire the root Blazor component to the <app> selector in wwwroot/index.html.
        appBuilder.RootComponents.Add<App>("app");

        var app = appBuilder.Build();

        // Store the main window instance for JSInvokable window controls.
        MediaMusic.Platform.WindowHelper.MainWindow = app.MainWindow;

        // Configure the chromeless main window.
        app.MainWindow
            .SetIconFile("wwwroot/favicon.ico")
            .SetTitle("MediaMusic")
            .SetUseOsDefaultSize(false)
            .SetSize(1200, 800)
            .SetMinSize(960, 600)
            .SetChromeless(true)
            .Center();

        // Surface unhandled exceptions to the user instead of dying silently.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            app.MainWindow.ShowMessage("Fatal exception", e.ExceptionObject?.ToString() ?? string.Empty);

        // NOTE: additional windows (MiniPlayer / DesktopLyrics) are created on
        // demand by Platform.WindowManager, each as its own PhotinoBlazorApp.
        app.Run();
    }
}
