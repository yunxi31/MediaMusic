using MediaMusic.Data.Models;

namespace MediaMusic.Audio;

/// <summary>
/// Core playback service (PRD §2.1). Will own the gapless double-buffer stream
/// alternation (BASSmix) and expose play/pause/seek/queue operations to the UI.
/// </summary>
public sealed class PlayerService
{
    private readonly BassEngine _engine;
    private readonly ILogger<PlayerService> _logger;
    private readonly AudioState _state = new();

    public PlayerService(BassEngine engine, ILogger<PlayerService> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    /// <summary>Read-only snapshot of current playback state.</summary>
    public AudioState State => _state;

    public void Play(Track? track)
    {
        // TODO: create BASS stream for track.FilePath (with gapless preload of next track
        //       via a mixer), start playback, update _state.CurrentTrack/DurationMs.
        if (!_engine.IsAvailable)
        {
            _logger.LogWarning("Play requested but BASS engine is not available.");
            return;
        }

        _state.CurrentTrack = track;
        _state.IsPlaying = true;
        OnStateChanged();
    }

    public void Pause()
    {
        // TODO: Bass.ChannelPause(_currentChannel).
        _state.IsPlaying = false;
        OnStateChanged();
    }

    public void Resume()
    {
        // TODO: Bass.Start() on the active channel.
        _state.IsPlaying = true;
        OnStateChanged();
    }

    public void Next() => Skip(+1);
    public void Previous() => Skip(-1);

    private void Skip(int direction)
    {
        // TODO: advance through the play queue with crossfade (see EffectsService).
        _logger.LogDebug("Skip {Direction} requested.", direction);
    }

    public void Seek(long positionMs)
    {
        // TODO: Bass.ChannelSetPosition(_currentChannel, pos, PositionFlags.Milliseconds).
        _state.PositionMs = positionMs;
        OnStateChanged();
    }

    public void SetVolume(double volume)
    {
        // TODO: Bass.ChannelSetAttribute(_currentChannel, ChannelAttribute.Volume, volume).
        _state.Volume = Math.Clamp(volume, 0, 1);
        OnStateChanged();
    }

    /// <summary>Raised whenever playback state changes so the UI can re-render.</summary>
    public event EventHandler? StateChanged;

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
