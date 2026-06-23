namespace MediaMusic.Audio;

/// <summary>Repeat mode for the play queue.</summary>
public enum RepeatMode
{
    /// <summary>No repeat — stop at end of queue.</summary>
    None,
    /// <summary>Repeat the entire queue.</summary>
    All,
    /// <summary>Repeat the current track.</summary>
    One,
}

/// <summary>Playback status flags surfaced to the UI through <see cref="AppState"/>.</summary>
public sealed class AudioState
{
    public bool IsPlaying { get; set; }
    public bool IsMuted { get; set; }
    public double Volume { get; set; } = 1.0;     // 0.0..1.0
    public long PositionMs { get; set; }
    public long DurationMs { get; set; }
    public Data.Models.Track? CurrentTrack { get; set; }

    // ── Playback mode flags ──
    public RepeatMode RepeatMode { get; set; } = RepeatMode.None;
    public bool IsShuffled { get; set; }
}
