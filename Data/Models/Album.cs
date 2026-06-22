namespace MediaMusic.Data.Models;

/// <summary>An album (collection of tracks), optionally linked to a primary artist.</summary>
public sealed class Album
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string NormalizedTitle { get; set; } = string.Empty;
    public long? ArtistId { get; set; }
    public int? Year { get; set; }
    public string? CoverPath { get; set; }
}
