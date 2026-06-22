using ManagedBass;

namespace MediaMusic.Audio;

/// <summary>
/// Owns the BASS audio engine lifecycle: device init, codec plugin loading and
/// shutdown. Native <c>bass*.dll</c> files are NOT distributed via NuGet (licensing)
/// and must be dropped into <c>native/bass/</c>; this stub tolerates their absence
/// so the skeleton still runs without audio.
/// </summary>
public sealed class BassEngine : IDisposable
{
    private readonly ILogger<BassEngine> _logger;
    private bool _initialized;
    private bool _disposed;

    public BassEngine(ILogger<BassEngine> logger) => _logger = logger;

    /// <summary>True once <see cref="Init"/> successfully initialized the BASS device.</summary>
    public bool IsAvailable => _initialized;

    /// <summary>
    /// Initializes the default output device and loads codec plugins.
    /// Fails soft: logs a warning and leaves <see cref="IsAvailable"/> false if the
    /// native BASS DLLs are missing, so the rest of the app keeps working.
    /// </summary>
    public void Init()
    {
        if (_initialized)
            return;

        try
        {
            // TODO: verify native bass.dll/bassmix.dll/bass_fx.dll are present in the
            //       output directory (copied from native/bass via .csproj) before init.
            if (!Bass.Init())
            {
                _logger.LogWarning("Bass.Init failed: {Error}", Bass.LastError);
                return;
            }

            LoadPlugins();
            _initialized = true;
            _logger.LogInformation("BASS engine initialized (version {Version}).", Bass.Version);
        }
        catch (DllNotFoundException ex)
        {
            _logger.LogWarning(ex,
                "Native BASS DLLs not found in native/bass/. Audio disabled until they are added.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BASS engine initialization failed. Audio disabled.");
        }
    }

    private void LoadPlugins()
    {
        // TODO: Bass.PluginLoad("bassflac.dll"); ... for ape/aac/wv.
        // Order matters: plugins must load after Bass.Init().
        foreach (var plugin in new[] { "bassflac.dll", "bass_ape.dll", "bass_aac.dll" })
        {
            try
            {
                var handle = Bass.PluginLoad(plugin);
                if (handle == 0)
                    _logger.LogWarning("Failed to load BASS plugin {Plugin}: {Error}", plugin, Bass.LastError);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load BASS plugin {Plugin}.", plugin);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (_initialized)
        {
            try { Bass.Free(); } catch { /* swallow during shutdown */ }
        }
        _disposed = true;
    }
}
