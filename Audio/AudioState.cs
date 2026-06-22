namespace MediaMusic.Audio;

/// <summary>Playback status flags surfaced to the UI through <see cref="AppState"/>.</summary>
public sealed class AudioState
{
    public bool IsPlaying { get; set; }
    public bool IsMuted { get; set; }
    public double Volume { get; set; } = 1.0;     // 0.0..1.0
    public long PositionMs { get; set; }
    public long DurationMs { get; set; }
    public Data.Models.Track? CurrentTrack { get; set; }
}
