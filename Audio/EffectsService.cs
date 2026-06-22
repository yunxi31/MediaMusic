namespace MediaMusic.Audio;

/// <summary>
/// Crossfade / fade-in / fade-out DSP (PRD §2.1). Crossfade duration is user
/// configurable in the 0–5000 ms range. Will drive linear volume ramps on the
/// BASS mixer streams during playback/pause/manual skip transitions.
/// </summary>
public sealed class EffectsService
{
    private readonly BassEngine _engine;
    private readonly ILogger<EffectsService> _logger;

    /// <summary>Crossfade duration in milliseconds (0 = disabled, max 5000).</summary>
    public int CrossfadeMs { get; set; } = 0;

    public EffectsService(BassEngine engine, ILogger<EffectsService> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    /// <summary>Linear volume ramp applied when starting/resuming playback.</summary>
    public Task FadeInAsync(int durationMs)
    {
        // TODO: slide channel volume 0 -> target over durationMs via Bass.ChannelSlideAttribute.
        _logger.LogDebug("FadeIn {Ms}ms (stub).", durationMs);
        return Task.CompletedTask;
    }

    /// <summary>Linear volume ramp applied when pausing or stopping.</summary>
    public Task FadeOutAsync(int durationMs)
    {
        // TODO: slide channel volume -> 0 over durationMs, then pause/stop.
        _logger.LogDebug("FadeOut {Ms}ms (stub).", durationMs);
        return Task.CompletedTask;
    }

    /// <summary>Crossfades between the outgoing and incoming track during a skip.</summary>
    public Task CrossfadeAsync(int durationMs)
    {
        // TODO: overlap two mixer streams, ramp outgoing down and incoming up simultaneously.
        _logger.LogDebug("Crossfade {Ms}ms (stub).", durationMs);
        return Task.CompletedTask;
    }
}
