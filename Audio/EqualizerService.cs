using MediaMusic.Data.Models;

namespace MediaMusic.Audio;

/// <summary>
/// Multi-band equalizer (PRD §2.1). Will build a chain of BASS_FX PEAKEQ DSP
/// filters and apply/save user presets (10+ bands, -12..+12 dB).
/// </summary>
public sealed class EqualizerService
{
    private readonly BassEngine _engine;
    private readonly ILogger<EqualizerService> _logger;

    // Standard 10-band frequencies (Hz). PRD requires 10+ bands.
    public static readonly double[] DefaultFrequencies =
        { 32, 64, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };

    public EqualizerService(BassEngine engine, ILogger<EqualizerService> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    /// <summary>Applies the given band gains (dB, -12..+12) to the active channel.</summary>
    public void ApplyBands(IEnumerable<EqBand> bands)
    {
        if (!_engine.IsAvailable)
            return;

        // TODO: for each band create/update a BASS_BFX_PEAKEQ DSP on the channel:
        //   var fx = Bass.ChannelSetFX(channel, EffectType.PeakEQ, 0);
        //   Bass.FXSetParameters(fx, new PeakEQParameters { fCenter = freq, fGain = gain, fBandwidth = 1.0f });
        _logger.LogDebug("ApplyBands: {Count} bands.", bands.Count());
    }

    public void ApplyPreset(EqPreset preset)
    {
        // TODO: deserialize preset.Bands JSON -> EqBand[] -> ApplyBands(...).
        _logger.LogDebug("ApplyPreset: {Name}.", preset.Name);
    }

    public void Disable()
    {
        // TODO: remove all EQ DSP handles from the channel.
    }
}
