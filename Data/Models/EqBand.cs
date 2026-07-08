namespace MediaMusic.Data.Models;

/// <summary>
/// Represents a single equalizer band with frequency, gain, and bandwidth.
/// </summary>
public sealed class EqBand
{
    /// <summary>
    /// Gets or sets the center frequency in Hz (20-20000).
    /// </summary>
    public double Frequency { get; set; }

    /// <summary>
    /// Gets or sets the gain in dB (-12.0 to +12.0).
    /// </summary>
    public double Gain { get; set; }

    /// <summary>
    /// Gets or sets the bandwidth (Q factor), default 1.0.
    /// </summary>
    public double Bandwidth { get; set; } = 1.0;
}
