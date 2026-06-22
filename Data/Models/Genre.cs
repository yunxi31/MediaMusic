namespace MediaMusic.Data.Models;

/// <summary>A musical genre.</summary>
public sealed class Genre
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
