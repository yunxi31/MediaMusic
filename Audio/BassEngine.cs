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
    private readonly List<int> _pluginHandles = new();
    private bool _initialized;
    private bool _disposed;

    public BassEngine(ILogger<BassEngine> logger) => _logger = logger;

    /// <summary>True once <see cref="Init"/> successfully initialized the BASS device.</summary>
    public bool IsAvailable => _initialized;

    private bool VerifyNativeDlls()
    {
        var requiredDlls = new[] { "bass.dll", "bassmix.dll", "bass_fx.dll" };
        var appDir = AppContext.BaseDirectory;
        
        foreach (var dll in requiredDlls)
        {
            var path = Path.Combine(appDir, dll);
            if (!File.Exists(path))
            {
                _logger.LogWarning("Missing required BASS DLL: {Dll}", dll);
                return false;
            }
        }
        return true;
    }

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
            if (!VerifyNativeDlls())
            {
                _logger.LogWarning("BASS native DLLs verification failed. Audio disabled.");
                return;
            }

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
                "Native BASS DLLs not found. Audio disabled until they are added.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BASS engine initialization failed. Audio disabled.");
        }
    }

    private void LoadPlugins()
    {
        var plugins = new[] { "bassflac.dll", "bass_ape.dll", "bass_aac.dll" };
        var appDir = AppContext.BaseDirectory;
        _pluginHandles.Clear();

        foreach (var plugin in plugins)
        {
            try
            {
                var path = Path.Combine(appDir, plugin);
                if (!File.Exists(path))
                {
                    _logger.LogDebug("Optional BASS plugin not found: {Plugin}", plugin);
                    continue;
                }

                var handle = Bass.PluginLoad(path);
                if (handle == 0)
                {
                    _logger.LogWarning("Failed to load BASS plugin {Plugin}: {Error}", plugin, Bass.LastError);
                }
                else
                {
                    _pluginHandles.Add(handle);
                    _logger.LogInformation("Loaded BASS plugin: {Plugin} (handle {Handle})", plugin, handle);
                }
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
            try
            {
                foreach (var handle in _pluginHandles)
                {
                    Bass.PluginFree(handle);
                }
                _pluginHandles.Clear();
                Bass.Free();
            }
            catch { /* swallow during shutdown */ }
        }
        _disposed = true;
    }
}
