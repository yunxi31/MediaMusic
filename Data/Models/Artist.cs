namespace MediaMusic.Data.Models;

/// <summary>A performing artist.</summary>
public sealed class Artist
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? CoverPath { get; set; }
}
