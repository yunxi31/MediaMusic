using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ManagedBass;

namespace MediaMusic.Audio;

/// <summary>
/// Crossfade / fade-in / fade-out DSP (PRD §2.1). Crossfade duration is user
/// configurable in the 0–5000 ms range. Will drive volume ramps on the
/// BASS streams during playback/pause/manual skip transitions.
/// </summary>
public sealed class EffectsService : IDisposable
{
    private readonly BassEngine _engine;
    private readonly ILogger<EffectsService> _logger;
    private readonly SemaphoreSlim _fadeLock = new(1, 1);
    private bool _disposed;

    /// <summary>Crossfade duration in milliseconds (0 = disabled, max 5000).</summary>
    public int CrossfadeMs { get; set; } = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="EffectsService"/> class.
    /// </summary>
    public EffectsService(BassEngine engine, ILogger<EffectsService> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    /// <summary>Linear volume ramp applied when starting/resuming playback.</summary>
    /// <param name="channelHandle">The BASS channel handle.</param>
    /// <param name="durationMs">The duration in milliseconds (0-5000).</param>
    /// <param name="targetVolume">The target volume.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if durationMs is out of bounds.</exception>
    public async Task FadeInAsync(int channelHandle, int durationMs, double targetVolume, CancellationToken ct = default)
    {
        if (durationMs < 0 || durationMs > 5000)
            throw new ArgumentOutOfRangeException(nameof(durationMs), "Duration must be between 0 and 5000 milliseconds.");

        if (!_engine.IsAvailable || channelHandle == 0)
        {
            _logger.LogDebug("BASS engine not available, skipping fade-in.");
            return;
        }

        await _fadeLock.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();

            _logger.LogDebug("Starting fade-in on channel {Channel} over {Ms}ms to {Volume}.", channelHandle, durationMs, targetVolume);
            
            // Set initial volume to 0
            Bass.ChannelSetAttribute(channelHandle, ChannelAttribute.Volume, 0f);
            
            // Start playing the channel
            if (Bass.ChannelIsActive(channelHandle) != PlaybackState.Playing)
            {
                Bass.ChannelPlay(channelHandle);
            }

            // Start slide
            if (!Bass.ChannelSlideAttribute(channelHandle, ChannelAttribute.Volume, (float)targetVolume, durationMs))
            {
                _logger.LogWarning("Failed to slide volume for channel {Channel}: {Error}", channelHandle, Bass.LastError);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error during FadeInAsync for channel {Channel}", channelHandle);
        }
        finally
        {
            _fadeLock.Release();
        }
    }

    /// <summary>Linear volume ramp applied when pausing or stopping.</summary>
    /// <param name="channelHandle">The BASS channel handle.</param>
    /// <param name="durationMs">The duration in milliseconds (0-5000).</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if durationMs is out of bounds.</exception>
    public async Task FadeOutAsync(int channelHandle, int durationMs, CancellationToken ct = default)
    {
        if (durationMs < 0 || durationMs > 5000)
            throw new ArgumentOutOfRangeException(nameof(durationMs), "Duration must be between 0 and 5000 milliseconds.");

        if (!_engine.IsAvailable || channelHandle == 0)
        {
            _logger.LogDebug("BASS engine not available, skipping fade-out.");
            return;
        }

        await _fadeLock.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();

            _logger.LogDebug("Starting fade-out on channel {Channel} over {Ms}ms.", channelHandle, durationMs);
            
            if (!Bass.ChannelSlideAttribute(channelHandle, ChannelAttribute.Volume, 0f, durationMs))
            {
                _logger.LogWarning("Failed to start fade-out slide for channel {Channel}: {Error}", channelHandle, Bass.LastError);
                Bass.ChannelPause(channelHandle);
                return;
            }

            // Wait until volume reaches 0 or cancel
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < durationMs)
            {
                await Task.Delay(50, ct);
                if (ct.IsCancellationRequested)
                {
                    // Restore volume immediately if cancelled
                    Bass.ChannelSlideAttribute(channelHandle, ChannelAttribute.Volume, 1.0f, 0);
                    ct.ThrowIfCancellationRequested();
                }
            }

            Bass.ChannelPause(channelHandle);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Fade-out operation on channel {Channel} was cancelled.", channelHandle);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during FadeOutAsync for channel {Channel}", channelHandle);
        }
        finally
        {
            _fadeLock.Release();
        }
    }

    /// <summary>Crossfades between the outgoing and incoming track during a skip.</summary>
    /// <param name="outgoingChannel">The outgoing channel handle.</param>
    /// <param name="incomingChannel">The incoming channel handle.</param>
    /// <param name="durationMs">The duration in milliseconds (0-5000).</param>
    /// <param name="targetVolume">The target volume for the incoming channel.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if durationMs is out of bounds.</exception>
    public async Task CrossfadeAsync(int outgoingChannel, int incomingChannel, int durationMs, double targetVolume, CancellationToken ct = default)
    {
        if (durationMs < 0 || durationMs > 5000)
            throw new ArgumentOutOfRangeException(nameof(durationMs), "Duration must be between 0 and 5000 milliseconds.");

        if (!_engine.IsAvailable)
        {
            _logger.LogDebug("BASS engine not available, skipping crossfade.");
            return;
        }

        if (CrossfadeMs == 0 || outgoingChannel == 0 || incomingChannel == 0)
        {
            _logger.LogDebug("Crossfade disabled or invalid channel, switching instantly.");
            if (incomingChannel != 0)
            {
                Bass.ChannelSetAttribute(incomingChannel, ChannelAttribute.Volume, (float)targetVolume);
                Bass.ChannelPlay(incomingChannel);
            }
            if (outgoingChannel != 0)
            {
                Bass.ChannelPause(outgoingChannel);
            }
            return;
        }

        await _fadeLock.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();

            _logger.LogDebug("Starting crossfade outgoing {OutChannel} -> incoming {InChannel} over {Ms}ms.", outgoingChannel, incomingChannel, durationMs);

            // Prepare incoming volume at 0
            Bass.ChannelSetAttribute(incomingChannel, ChannelAttribute.Volume, 0f);
            Bass.ChannelPlay(incomingChannel);

            // Start slide outgoing to 0 and incoming to target
            Bass.ChannelSlideAttribute(outgoingChannel, ChannelAttribute.Volume, 0f, durationMs);
            Bass.ChannelSlideAttribute(incomingChannel, ChannelAttribute.Volume, (float)targetVolume, durationMs);

            // Wait for slide to complete
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < durationMs)
            {
                await Task.Delay(50, ct);
                
                // If outgoing ends early, stop waiting
                if (Bass.ChannelIsActive(outgoingChannel) == PlaybackState.Stopped)
                {
                    break;
                }

                if (ct.IsCancellationRequested)
                {
                    // Restore incoming volume, stop outgoing
                    Bass.ChannelSlideAttribute(incomingChannel, ChannelAttribute.Volume, (float)targetVolume, 0);
                    Bass.ChannelSlideAttribute(outgoingChannel, ChannelAttribute.Volume, 0f, 0);
                    Bass.ChannelPause(outgoingChannel);
                    ct.ThrowIfCancellationRequested();
                }
            }

            Bass.ChannelPause(outgoingChannel);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Crossfade operation was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CrossfadeAsync.");
        }
        finally
        {
            _fadeLock.Release();
        }
    }

    /// <summary>
    /// Disposes the service resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _fadeLock.Dispose();
        _disposed = true;
    }
}
