using ManagedBass;
using MediaMusic.Data.Models;
using MediaMusic.Data.Repositories;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using MediaMusic.Services;


namespace MediaMusic.Audio;

/// <summary>
/// Core playback service (PRD §2.1). Owns the active audio stream and exposes
/// play / pause / resume / seek / volume / queue / shuffle / repeat / favourite
/// operations to the UI.
/// <para>
/// Engine selection: uses <see cref="BassEngine"/> (BASS) when the native DLLs
/// are present; otherwise falls back to <b>NAudio</b> (MediaFoundation) so the
/// app produces sound out-of-the-box without requiring manual bass.dll download.
/// </para>
/// </summary>
public sealed class PlayerService : IDisposable
{
    private readonly BassEngine _engine;
    private readonly TrackRepository _trackRepo;
    private readonly EqualizerService _equalizerService;
    private readonly SettingsService _settingsService;
    private readonly ILogger<PlayerService> _logger;
    private readonly AudioState _state = new();
    private readonly Random _rng = new();

    private readonly PlayHistoryService _playHistoryService;
    private bool _playedHistoryRecorded;

    // ── BASS state ──
    private int _bassChannel;
    private int _endSyncHandle;

    // ── NAudio state (fallback engine) ──
    private WaveOutEvent? _waveOut;
    private WaveStream? _waveStream;
    private VolumeSampleProvider? _volumeProvider;

    // ── Play queue ──
    private readonly List<Track> _queue = new();
    private int _queueIndex = -1;
    private int[]? _shuffleOrder;

    // ── Position polling timer ──
    private readonly System.Threading.Timer _positionTimer;
    private bool _disposed;

    public PlayerService(
        BassEngine engine,
        TrackRepository trackRepo,
        PlayHistoryService playHistoryService,
        EqualizerService equalizerService,
        SettingsService settingsService,
        ILogger<PlayerService> logger)
    {
        _engine = engine;
        _trackRepo = trackRepo;
        _playHistoryService = playHistoryService;
        _equalizerService = equalizerService;
        _settingsService = settingsService;
        _logger = logger;
        _positionTimer = new System.Threading.Timer(PollPosition, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>True when the BASS engine native DLLs are loaded.</summary>
    private bool UseBass => _engine.IsAvailable;

    /// <summary>The active BASS channel handle.</summary>
    public int ActiveChannelHandle => _bassChannel;

    /// <summary>Read-only snapshot of current playback state.</summary>
    public AudioState State => _state;

    // ════════════════════════════════════════════════════════════
    //  Public API — Playback control
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Plays the given track. If the track is already in the queue, jumps to it;
    /// otherwise replaces the queue with just this track.
    /// </summary>
    public void Play(Track? track)
    {
        if (track == null) return;

        // If same track and was paused → resume
        if (_state.CurrentTrack?.FilePath == track.FilePath && !_state.IsPlaying)
        {
            Resume();
            return;
        }

        // Set up queue: if track is in queue, use its index; else single-track queue
        var idx = _queue.FindIndex(t => t.FilePath == track.FilePath);
        if (idx >= 0)
            _queueIndex = idx;
        else
        {
            _queue.Clear();
            _queue.Add(track);
            _queueIndex = 0;
        }

        PlayCurrentTrack();
    }

    /// <summary>Enqueues a list of tracks and starts playing the first one.</summary>
    public void PlayQueue(IReadOnlyList<Track> tracks, int startIndex = 0)
    {
        if (tracks.Count == 0) return;
        _queue.Clear();
        _queue.AddRange(tracks);
        _queueIndex = Math.Clamp(startIndex, 0, tracks.Count - 1);
        BuildShuffleOrder();
        PlayCurrentTrack();
    }

    public void Pause()
    {
        if (UseBass)
        {
            if (_bassChannel != 0 && Bass.ChannelIsActive(_bassChannel) == ManagedBass.PlaybackState.Playing)
                Bass.ChannelPause(_bassChannel);
        }
        else
        {
            _waveOut?.Pause();
        }

        _state.IsPlaying = false;
        StopPositionTimer();
        OnStateChanged();
    }

    public void Resume()
    {
        if (_state.CurrentTrack == null) return;

        if (UseBass)
        {
            if (_bassChannel != 0 && Bass.ChannelIsActive(_bassChannel) == ManagedBass.PlaybackState.Paused)
                Bass.ChannelPlay(_bassChannel);
        }
        else
        {
            _waveOut?.Play();
        }

        _state.IsPlaying = true;
        StartPositionTimer();
        OnStateChanged();
    }

    public void Next() => Skip(+1);
    public void Previous() => Skip(-1);

    public void Seek(long positionMs)
    {
        if (positionMs < 0) positionMs = 0;
        if (positionMs > _state.DurationMs) positionMs = _state.DurationMs;

        if (UseBass)
        {
            if (_bassChannel != 0)
                Bass.ChannelSetPosition(_bassChannel, Bass.ChannelSeconds2Bytes(_bassChannel, positionMs / 1000.0));
        }
        else
        {
            if (_waveStream != null)
                _waveStream.CurrentTime = TimeSpan.FromMilliseconds(positionMs);
        }

        _state.PositionMs = positionMs;
        OnStateChanged();
    }

    public void SetVolume(double volume)
    {
        // Allow up to 2.0 (200% / +6 dB software boost)
        var v = Math.Clamp(volume, 0, 2.0);
        _state.Volume = v;
        _state.IsMuted = v == 0;

        ApplyVolume(_state.IsMuted ? 0 : v);
        OnStateChanged();
    }

    // ════════════════════════════════════════════════════════════
    //  Public API — Mode toggles
    // ════════════════════════════════════════════════════════════

    /// <summary>Toggles mute on/off, preserving the volume slider value.</summary>
    public void ToggleMute()
    {
        if (_state.Volume > 0)
        {
            // Store current volume, set to 0
            _state.IsMuted = true;
            ApplyVolume(0);
        }
        else
        {
            // Restore to full volume
            _state.IsMuted = false;
            ApplyVolume(1.0);
        }
        OnStateChanged();
    }

    /// <summary>Cycles repeat mode: None → All → One → None...</summary>
    public void CycleRepeatMode()
    {
        _state.RepeatMode = _state.RepeatMode switch
        {
            RepeatMode.None => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            RepeatMode.One => RepeatMode.None,
            _ => RepeatMode.None,
        };
        OnStateChanged();
    }

    /// <summary>Toggles shuffle mode on/off and rebuilds the shuffle order.</summary>
    public void ToggleShuffle()
    {
        _state.IsShuffled = !_state.IsShuffled;
        if (_state.IsShuffled)
            BuildShuffleOrder();
        else
            _shuffleOrder = null;
        OnStateChanged();
    }

    /// <summary>Toggles favourite status for the current track.</summary>
    public async void ToggleFavourite()
    {
        var track = _state.CurrentTrack;
        if (track is null || track.Id <= 0) return;

        try
        {
            var newValue = await _trackRepo.ToggleFavouriteAsync(track.Id);
            track.IsFavourite = newValue;
            OnStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle favourite for track {Id}", track.Id);
        }
    }

    public async Task IncrementPlayCountAsync()
    {
        var track = _state.CurrentTrack;
        if (track is null || track.Id <= 0) return;
        try
        {
            await _trackRepo.IncrementPlayCountAsync(track.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment play count for track {Id}", track.Id);
        }
    }

    /// <summary>Raised whenever playback state changes so the UI can re-render.</summary>
    public event EventHandler? StateChanged;

    // ════════════════════════════════════════════════════════════
    //  Internal playback logic
    // ════════════════════════════════════════════════════════════

    private void PlayCurrentTrack()
    {
        if (_queueIndex < 0 || _queueIndex >= _queue.Count) return;
        var track = _queue[_queueIndex];

        // Skip non-file (mock/sample) tracks — just update state
        if (track.FilePath.StartsWith("sample://") || !File.Exists(track.FilePath))
        {
            _logger.LogDebug("Playing sample/non-file track: {Title}", track.Title);
            CleanupPlayback();
            _state.CurrentTrack = track;
            _state.IsPlaying = true;
            _state.PositionMs = 0;
            _state.DurationMs = track.DurationMs ?? 240000;
            OnStateChanged();
            return;
        }

        _playedHistoryRecorded = false;
        CleanupPlayback();

        try
        {
            if (UseBass)
                PlayWithBass(track);
            else
                PlayWithNaudio(track);

            _state.CurrentTrack = track;
            _state.IsPlaying = true;
            _state.PositionMs = 0;
            StartPositionTimer();
            OnStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to play {File}", track.FilePath);
            _state.CurrentTrack = track;
            _state.IsPlaying = false;
            OnStateChanged();
        }
    }

    // ── Skip with shuffle / repeat awareness ──

    private void Skip(int direction)
    {
        if (_queue.Count == 0)
        {
            _logger.LogDebug("Skip requested but queue is empty.");
            return;
        }

        // ── Repeat One: just restart current track ──
        if (direction > 0 && _state.RepeatMode == RepeatMode.One)
        {
            Seek(0);
            return;
        }

        // ── Shuffle: pick a random track ──
        if (direction > 0 && _state.IsShuffled && _shuffleOrder != null && _shuffleOrder.Length > 1)
        {
            _queueIndex = NextShuffledIndex();
            PlayCurrentTrack();
            return;
        }

        // ── Normal linear skip ──
        _queueIndex += direction;

        if (_queueIndex < 0)
        {
            _queueIndex = _state.RepeatMode == RepeatMode.All ? _queue.Count - 1 : 0;
        }
        else if (_queueIndex >= _queue.Count)
        {
            if (_state.RepeatMode == RepeatMode.All)
                _queueIndex = 0;
            else
            {
                _queueIndex = _queue.Count - 1;
                // End of queue — stop
                _state.IsPlaying = false;
                _state.PositionMs = _state.DurationMs;
                StopPositionTimer();
                OnStateChanged();
                return;
            }
        }

        PlayCurrentTrack();
    }

    private int NextShuffledIndex()
    {
        if (_shuffleOrder == null || _shuffleOrder.Length == 0) return _queueIndex;

        // Pick the next index in the shuffle order that differs from current
        for (int i = 0; i < _shuffleOrder.Length; i++)
        {
            if (_shuffleOrder[i] == _queueIndex && i + 1 < _shuffleOrder.Length)
                return _shuffleOrder[i + 1];
        }
        // Current not found in order — pick a random one
        return _shuffleOrder[_rng.Next(_shuffleOrder.Length)];
    }

    // ── Shuffle order ──

    private void BuildShuffleOrder()
    {
        if (_queue.Count == 0) { _shuffleOrder = null; return; }

        var order = new int[_queue.Count];
        for (int i = 0; i < _queue.Count; i++) order[i] = i;

        // Fisher-Yates shuffle
        for (int i = _queue.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        _shuffleOrder = order;
    }

    // ── BASS engine playback ──

    private void PlayWithBass(Track track)
    {
        _bassChannel = Bass.CreateStream(track.FilePath);
        if (_bassChannel == 0)
        {
            _logger.LogWarning("BASS CreateStream failed for {File}: {Error}", track.FilePath, Bass.LastError);
            throw new InvalidOperationException($"BASS cannot open: {track.FilePath}");
        }

        double seconds = Bass.ChannelBytes2Seconds(_bassChannel, Bass.ChannelGetLength(_bassChannel));
        _state.DurationMs = seconds > 0 ? (long)(seconds * 1000) : (track.DurationMs ?? 0);

        ApplyVolume(_state.IsMuted ? 0 : _state.Volume);

        _ = ApplyEqualizerToActiveChannelAsync();

        _endSyncHandle = Bass.ChannelSetSync(_bassChannel, SyncFlags.End, 0, OnBassStreamEnd);
        Bass.ChannelPlay(_bassChannel);
        _logger.LogInformation("BASS playing: {Title}", track.Title);
    }

    private async Task ApplyEqualizerToActiveChannelAsync()
    {
        var channel = _bassChannel;
        if (channel == 0) return;
        try
        {
            bool eqEnabled = await _settingsService.GetAsync<bool>("eq_enabled", false);
            if (eqEnabled)
            {
                var bandsJson = await _settingsService.GetAsync("eq_bands");
                if (!string.IsNullOrEmpty(bandsJson))
                {
                    var bands = System.Text.Json.JsonSerializer.Deserialize<List<EqBand>>(bandsJson);
                    if (bands != null)
                    {
                        _equalizerService.ApplyBands(channel, bands);
                        return;
                    }
                }
            }
            _equalizerService.Disable(channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply equalizer to active channel {Channel}.", channel);
        }
    }

    private void OnBassStreamEnd(int handle, int channel, int data, IntPtr user)
    {
        var trackId = _state.CurrentTrack?.Id ?? 0;
        if (!_playedHistoryRecorded && trackId > 0)
        {
            _playedHistoryRecorded = true;
            _ = Task.Run(async () =>
            {
                try { await _playHistoryService.RecordPlayAsync(trackId); } catch { }
            });
        }

        _ = Task.Run(() =>
        {
            // Respect repeat-one: restart current track
            if (_state.RepeatMode == RepeatMode.One)
            {
                if (UseBass && _bassChannel != 0)
                {
                    Bass.ChannelSetPosition(_bassChannel, 0);
                    Bass.ChannelPlay(_bassChannel);
                    _state.PositionMs = 0;
                    OnStateChanged();
                }
                return;
            }

            if (_queueIndex < _queue.Count - 1 || _state.RepeatMode == RepeatMode.All)
                Next();
            else
            {
                _state.IsPlaying = false;
                _state.PositionMs = _state.DurationMs;
                StopPositionTimer();
                OnStateChanged();
            }
        });
    }

    // ── NAudio fallback playback ──

    private void PlayWithNaudio(Track track)
    {
        _waveStream = new MediaFoundationReader(track.FilePath);
        // Use VolumeSampleProvider so we can boost beyond 1.0 (software gain)
        var sampleProvider = _waveStream.ToSampleProvider();
        _volumeProvider = new VolumeSampleProvider(sampleProvider)
        {
            Volume = (float)Math.Clamp(_state.IsMuted ? 0 : _state.Volume, 0, float.MaxValue)
        };
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_volumeProvider);
        _waveOut.PlaybackStopped += OnNaudioStopped;
        _waveOut.Play();

        _state.DurationMs = (long)_waveStream.TotalTime.TotalMilliseconds;
        if (_state.DurationMs <= 0)
            _state.DurationMs = track.DurationMs ?? 0;

        _logger.LogInformation("NAudio playing: {Title}", track.Title);
    }

    private void OnNaudioStopped(object? sender, StoppedEventArgs e)
    {
        if (_waveStream == null) return;

        var trackId = _state.CurrentTrack?.Id ?? 0;
        if (!_playedHistoryRecorded && trackId > 0 && e.Exception == null)
        {
            _playedHistoryRecorded = true;
            _ = Task.Run(async () =>
            {
                try { await _playHistoryService.RecordPlayAsync(trackId); } catch { }
            });
        }

        // If stopped due to an error, don't attempt to loop
        if (e.Exception != null)
        {
            _logger.LogError(e.Exception, "NAudio playback stopped due to an error.");
            Next();
            return;
        }

        // Respect repeat-one
        if (_state.RepeatMode == RepeatMode.One)
        {
            try
            {
                _waveStream.CurrentTime = TimeSpan.Zero;
                _waveOut?.Play();
                _state.PositionMs = 0;
                OnStateChanged();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to repeat track in NAudio");
                Next();
            }
            return;
        }

        if (_queueIndex < _queue.Count - 1 || _state.RepeatMode == RepeatMode.All)
            Next();
        else
        {
            _state.IsPlaying = false;
            _state.PositionMs = _state.DurationMs;
            StopPositionTimer();
            OnStateChanged();
        }
    }

    // ── Volume helper ──

    private void ApplyVolume(double v)
    {
        // Allow up to 2.0 for software boost; BASS supports >1.0 natively
        v = Math.Max(0, v);
        if (UseBass)
        {
            if (_bassChannel != 0)
                Bass.ChannelSetAttribute(_bassChannel, ChannelAttribute.Volume, (float)v);
        }
        else
        {
            // VolumeSampleProvider supports values >1.0 for amplification
            if (_volumeProvider != null)
                _volumeProvider.Volume = (float)v;
        }
    }

    // ── Position polling ──

    private void StartPositionTimer()
    {
        _positionTimer.Change(0, 500);
    }

    private void StopPositionTimer()
    {
        _positionTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void PollPosition(object? state)
    {
        if (!_state.IsPlaying) return;

        try
        {
            if (UseBass && _bassChannel != 0)
            {
                double pos = Bass.ChannelBytes2Seconds(_bassChannel, Bass.ChannelGetPosition(_bassChannel));
                _state.PositionMs = (long)(pos * 1000);
                OnStateChanged();
            }
            else if (_waveStream != null)
            {
                _state.PositionMs = (long)_waveStream.CurrentTime.TotalMilliseconds;
                OnStateChanged();
            }

            if (_state.DurationMs > 0 && _state.CurrentTrack != null)
            {
                double progress = (double)_state.PositionMs / _state.DurationMs;
                var trackId = _state.CurrentTrack.Id;
                if (progress >= 0.8 && !_playedHistoryRecorded && trackId > 0)
                {
                    _playedHistoryRecorded = true;
                    _ = Task.Run(async () =>
                    {
                        try { await _playHistoryService.RecordPlayAsync(trackId); } catch { }
                    });
                }
            }
        }
        catch
        {
            // Timer callback errors are non-fatal
        }
    }

    // ── Cleanup ──

    private void CleanupPlayback()
    {
        StopPositionTimer();

        if (UseBass)
        {
            if (_endSyncHandle != 0)
            {
                Bass.ChannelRemoveSync(_bassChannel, _endSyncHandle);
                _endSyncHandle = 0;
            }
            if (_bassChannel != 0)
            {
                Bass.StreamFree(_bassChannel);
                _bassChannel = 0;
            }
        }
        else
        {
            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= OnNaudioStopped;
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }
            _waveStream?.Dispose();
            _waveStream = null;
            _volumeProvider = null;
        }
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void NotifyStateChanged() => OnStateChanged();

    // ── IDisposable ──

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CleanupPlayback();
        _positionTimer.Dispose();
    }
}
