using MediaMusic.Audio;
using MediaMusic.Data;
using MediaMusic.Data.Repositories;
using MediaMusic.Library;
using MediaMusic.Lyrics;
using MediaMusic.Platform;

namespace MediaMusic.Services;

/// <summary>
/// Centralizes all MediaMusic dependency registrations. Called once from
/// <c>Program.cs</c>. Audio/data/library/lyrics/platform services are singletons
/// because BASS state and multi-window sync must be shared app-wide; repositories
/// are scoped per Blazor circuit.
/// </summary>
public static class ServiceRegistration
{
    public static void Register(IServiceCollection services)
    {
        // Audio engine (BASS global state must be a singleton)
        services.AddSingleton<BassEngine>();
        services.AddSingleton<PlayerService>();
        services.AddSingleton<EqualizerService>();
        services.AddSingleton<EffectsService>();
        services.AddSingleton<SearchService>();
        services.AddSingleton<PlayHistoryService>();

        // Persistence
        services.AddSingleton<DbConnectionFactory>();
        services.AddSingleton<SeedData>();
        services.AddSingleton<DbInitializer>();
        services.AddScoped<TrackRepository>();

        services.AddScoped<AlbumRepository>();
        services.AddScoped<ArtistRepository>();
        services.AddScoped<GenreRepository>();
        services.AddScoped<PlaylistRepository>();
        services.AddScoped<EqPresetRepository>();
        services.AddScoped<SettingsRepository>();

        // Library + metadata
        services.AddSingleton<MetadataReader>();
        services.AddSingleton<MetadataEditor>();
        services.AddSingleton<LibraryScanner>();
        services.AddSingleton<DragDropHandler>();
        services.AddSingleton<LibraryService>();

        // Lyrics
        services.AddSingleton<LrcParser>();
        services.AddSingleton<LyricsService>();

        // Platform (Win32 integrations)
        services.AddSingleton<WindowManager>();
        services.AddSingleton<GlobalHotkeyService>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<WindowDragService>();
        services.AddSingleton<ClickThroughService>();

        // App-level services & shared state
        services.AddSingleton<AppState>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<SettingsService>();
    }
}
