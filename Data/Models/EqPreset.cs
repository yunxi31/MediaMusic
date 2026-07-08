namespace MediaMusic.Data.Models;

/// <summary>
/// A saved equalizer preset. <see cref="Bands"/> holds the per-band gains as JSON:
/// <code>[{ "freq": 60, "gain": 3.0 }, ...]</code> (10+ bands).
/// </summary>
public sealed class EqPreset
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Bands { get; set; } = "[]";
    public bool IsBuiltIn { get; set; }
    public string? CreatedAt { get; set; }
}

