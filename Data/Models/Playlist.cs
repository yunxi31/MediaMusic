namespace MediaMusic.Data.Models;

/// <summary>A user-created playlist.</summary>
public sealed class Playlist
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CreatedAt { get; set; }
}
