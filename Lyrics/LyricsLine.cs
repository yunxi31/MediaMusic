namespace MediaMusic.Lyrics;

/// <summary>A single line of timed lyrics.</summary>
public sealed record LyricsLine(TimeSpan Time, string Text);
