namespace MediaMusic.Data.Models;

/// <summary>A parsed audio file indexed in the library.</summary>
public sealed class Track
{
    public long Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? Title { get; set; }
    public long? ArtistId { get; set; }
    public long? AlbumId { get; set; }
    public long? GenreId { get; set; }
    public int? TrackNo { get; set; }
    public int? Year { get; set; }
    public long? DurationMs { get; set; }
    public int? BitRate { get; set; }
    public int? SampleRate { get; set; }
    public int? Channels { get; set; }
    public string? Format { get; set; }
    public string? CoverPath { get; set; }
    public string? DateAdded { get; set; }
    public string? LastPlayed { get; set; }
    public long PlayCount { get; set; }
    public bool IsFavourite { get; set; }

    // Navigation helpers (populated on demand by repository joins)
    public string? ArtistName { get; set; }
    public string? AlbumTitle { get; set; }
    public string? GenreName { get; set; }
}
