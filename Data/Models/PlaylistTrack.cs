namespace MediaMusic.Data.Models;

/// <summary>Association row linking a track into a playlist at a given sort position.</summary>
public sealed class PlaylistTrack
{
    public long PlaylistId { get; set; }
    public long TrackId { get; set; }
    public int SortOrder { get; set; }
}
