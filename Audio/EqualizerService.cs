using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ManagedBass;
using ManagedBass.Fx;
using MediaMusic.Data.Models;
using MediaMusic.Data.Repositories;

namespace MediaMusic.Audio;

/// <summary>
/// Multi-band equalizer (PRD §2.1). Will build a chain of BASS_FX PEAKEQ DSP
/// filters and apply/save user presets (10+ bands, -12..+12 dB).
/// </summary>
public sealed class EqualizerService : IDisposable
{
    private readonly BassEngine _engine;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EqualizerService> _logger;

    private readonly Dictionary<int, List<int>> _channelHandles = new();
    private readonly object _lock = new();
    private bool _disposed;

    // Standard 10-band frequencies (Hz). PRD requires 10+ bands.
    public static readonly double[] DefaultFrequencies =
        { 32, 64, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };

    /// <summary>
    /// Initializes a new instance of the <see cref="EqualizerService"/> class.
    /// </summary>
    public EqualizerService(BassEngine engine, IServiceProvider serviceProvider, ILogger<EqualizerService> logger)
    {
        _engine = engine;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>Applies the given band gains (dB, -12..+12) to the active channel.</summary>
    /// <param name="channelHandle">The active channel handle.</param>
    /// <param name="bands">The equalizer bands to apply.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if any band parameters are out of range.</exception>
    public void ApplyBands(int channelHandle, IEnumerable<EqBand> bands)
    {
        if (bands == null)
            return;

        if (!_engine.IsAvailable || channelHandle == 0)
        {
            _logger.LogDebug("BASS engine not available, skipping equalizer.");
            return;
        }

        // Validate bands
        foreach (var band in bands)
        {
            if (band.Gain < -12.0 || band.Gain > 12.0)
                throw new ArgumentOutOfRangeException(nameof(bands), $"Gain must be between -12.0 and +12.0 dB. Frequency: {band.Frequency}Hz, Gain: {band.Gain}dB.");
            if (band.Frequency < 20 || band.Frequency > 20000)
                throw new ArgumentOutOfRangeException(nameof(bands), $"Frequency must be between 20 and 20000 Hz. Frequency: {band.Frequency}Hz.");
        }

        lock (_lock)
        {
            Disable(channelHandle);

            var handles = new List<int>();
            int bandIndex = 0;

            foreach (var band in bands)
            {
                var fxHandle = Bass.ChannelSetFX(channelHandle, EffectType.PeakEQ, 0);
                if (fxHandle == 0)
                {
                    _logger.LogError("Failed to set PeakEQ FX for band {Freq}Hz: {Error}", band.Frequency, Bass.LastError);
                    continue;
                }

                var parameters = new PeakEQParameters
                {
                    fCenter = (float)band.Frequency,
                    fGain = (float)band.Gain,
                    fBandwidth = (float)band.Bandwidth,
                    fQ = 0f,
                    lBand = bandIndex++,
                    lChannel = FXChannelFlags.All
                };

                if (!Bass.FXSetParameters(fxHandle, parameters))
                {
                    _logger.LogError("Failed to set PeakEQ parameters for band {Freq}Hz: {Error}", band.Frequency, Bass.LastError);
                    Bass.ChannelRemoveFX(channelHandle, fxHandle);
                    continue;
                }

                handles.Add(fxHandle);
            }

            if (handles.Count > 0)
            {
                _channelHandles[channelHandle] = handles;
                _logger.LogInformation("Successfully applied {Count} EQ bands to channel {Channel}.", handles.Count, channelHandle);
            }
        }
    }

    /// <summary>Applies a saved equalizer preset by ID.</summary>
    /// <param name="channelHandle">The active channel handle.</param>
    /// <param name="presetId">The preset ID.</param>
    public async Task ApplyPresetAsync(int channelHandle, long presetId)
    {
        if (!_engine.IsAvailable || channelHandle == 0)
            return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var presetRepo = scope.ServiceProvider.GetRequiredService<EqPresetRepository>();
            var preset = await presetRepo.GetByIdAsync(presetId);

            if (preset == null)
            {
                _logger.LogWarning("Preset with ID {Id} was not found.", presetId);
                return;
            }

            var bands = JsonSerializer.Deserialize<EqBand[]>(preset.Bands, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (bands == null || bands.Length == 0)
            {
                _logger.LogWarning("Preset {Name} (ID {Id}) contains no valid band data.", preset.Name, presetId);
                return;
            }

            ApplyBands(channelHandle, bands);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize EQ preset bands for preset ID {Id}.", presetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while applying EQ preset ID {Id}.", presetId);
        }
    }

    /// <summary>Removes all EQ DSP handles from the specified channel.</summary>
    /// <param name="channelHandle">The channel handle.</param>
    public void Disable(int channelHandle)
    {
        if (channelHandle == 0)
            return;

        lock (_lock)
        {
            if (_channelHandles.TryGetValue(channelHandle, out var handles))
            {
                foreach (var handle in handles)
                {
                    if (!Bass.ChannelRemoveFX(channelHandle, handle))
                    {
                        _logger.LogWarning("Failed to remove PeakEQ FX handle {Handle} from channel {Channel}: {Error}", handle, channelHandle, Bass.LastError);
                    }
                }
                _channelHandles.Remove(channelHandle);
                _logger.LogInformation("Disabled equalizer effects for channel {Channel}.", channelHandle);
            }
        }
    }

    /// <summary>
    /// Disposes the service resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        lock (_lock)
        {
            foreach (var kvp in _channelHandles)
            {
                var channelHandle = kvp.Key;
                foreach (var handle in kvp.Value)
                {
                    Bass.ChannelRemoveFX(channelHandle, handle);
                }
            }
            _channelHandles.Clear();
        }
        _disposed = true;
    }
}
