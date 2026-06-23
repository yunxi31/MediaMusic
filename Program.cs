using MediaMusic.Audio;
using MediaMusic.Platform;
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
        WindowHelper.MainWindow = app.MainWindow;

        // Initialize the BASS audio engine (fails soft if native DLLs are absent).
        var bassEngine = app.Services.GetRequiredService<BassEngine>();
        bassEngine.Init();
        if (bassEngine.IsAvailable)
            Console.WriteLine("BASS engine initialized — using BASS for audio playback.");
        else
            Console.WriteLine("BASS native DLLs not found — falling back to NAudio for audio playback.");

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

        // Bootstrap global hotkeys from persisted settings.
        _ = Task.Run(async () =>
        {
            // SettingsRepository is scoped; create a short-lived scope to read initial values.
            using var scope   = app.Services.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
            var player   = app.Services.GetRequiredService<PlayerService>();
            var hotkeys  = app.Services.GetRequiredService<GlobalHotkeyService>();

            var enabled = await settings.GetAsync<string>("global_hotkeys_enabled", "true") ?? "true";
            if (enabled != "true") return;

            var bindPlayPause = await settings.GetAsync<string>("shortcut_play_pause", "Space")        ?? "Space";
            var bindNext      = await settings.GetAsync<string>("shortcut_next",       "Ctrl + Right") ?? "Ctrl + Right";
            var bindPrev      = await settings.GetAsync<string>("shortcut_prev",       "Ctrl + Left")  ?? "Ctrl + Left";
            var bindVolUp     = await settings.GetAsync<string>("shortcut_vol_up",     "Alt + Up")     ?? "Alt + Up";
            var bindVolDown   = await settings.GetAsync<string>("shortcut_vol_down",   "Alt + Down")   ?? "Alt + Down";

            hotkeys.Register("play_pause", bindPlayPause, () =>
            {
                if (player.State.IsPlaying) player.Pause();
                else player.Resume();
            });
            hotkeys.Register("next",     bindNext,    player.Next);
            hotkeys.Register("prev",     bindPrev,    player.Previous);
            hotkeys.Register("vol_up",   bindVolUp,   () => { /* wire to volume control when AppState.Volume is implemented */ });
            hotkeys.Register("vol_down", bindVolDown, () => { /* wire to volume control when AppState.Volume is implemented */ });

            hotkeys.SetEnabled(true);
        });

        // NOTE: additional windows (MiniPlayer / DesktopLyrics) are created on
        // demand by Platform.WindowManager, each as its own PhotinoBlazorApp.
        app.Run();
    }
}
