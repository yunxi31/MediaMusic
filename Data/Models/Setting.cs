namespace MediaMusic.Data.Models;

/// <summary>A key/value application setting persisted in the Settings table.</summary>
public sealed class Setting
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}
