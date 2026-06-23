using ManagedBass;
using MediaMusic.Data.Models;
using TagLib;

namespace MediaMusic.Library;

/// <summary>
/// Reads audio metadata (ID3, Vorbis comments, APE tags) and embedded cover art
/// for a file (PRD §2.2). Uses <c>TagLibSharp</c> for uniform tag/property
/// extraction across FLAC/APE/WAV/MP3/AAC/OGG, and <c>ManagedBass</c> decoding
/// for precise technical parameters when the BASS engine is available.
/// </summary>
public sealed class MetadataReader
{
    private readonly ILogger<MetadataReader> _logger;

    public MetadataReader(ILogger<MetadataReader> logger) => _logger = logger;

    /// <summary>Extracts metadata + technical info for the given audio file.</summary>
    public Track Read(string filePath)
    {
        var info = new Track
        {
            FilePath = filePath,
            Title = Path.GetFileNameWithoutExtension(filePath),
            Format = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant(),
            DateAdded = DateTime.UtcNow.ToString("o")
        };

        // ── Primary: TagLibSharp for tags + properties ──
        TagLib.File? tagFile = null;
        try
        {
            tagFile = TagLib.File.Create(filePath);
            var tag = tagFile.Tag;

            if (!string.IsNullOrWhiteSpace(tag.Title))
                info.Title = tag.Title.Trim();

            var artist = tag.FirstAlbumArtist;
            if (string.IsNullOrWhiteSpace(artist))
                artist = tag.FirstPerformer;
            if (!string.IsNullOrWhiteSpace(artist))
                info.ArtistName = artist.Trim();

            if (!string.IsNullOrWhiteSpace(tag.Album))
                info.AlbumTitle = tag.Album.Trim();

            var genre = tag.FirstGenre;
            if (!string.IsNullOrWhiteSpace(genre))
                info.GenreName = genre.Trim();

            if (tag.Year > 0)
                info.Year = (int)tag.Year;

            if (tag.Track > 0)
                info.TrackNo = (int)tag.Track;

            if (tag.TrackCount > 0)
                info.TotalTracks = (int)tag.TrackCount;

            // Technical properties from TagLib
            var props = tagFile.Properties;
            if (props != null)
            {
                if (props.Duration > TimeSpan.Zero)
                    info.DurationMs = (long)props.Duration.TotalMilliseconds;

                if (props.AudioSampleRate > 0)
                    info.SampleRate = props.AudioSampleRate;

                if (props.AudioChannels > 0)
                    info.Channels = props.AudioChannels;

                if (props.AudioBitrate > 0)
                    info.BitRate = props.AudioBitrate;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "TagLibSharp could not read {File}, falling back to BASS.", filePath);
        }
        finally
        {
            tagFile?.Dispose();
        }

        // ── Secondary: BASS decoding for more accurate technical info ──
        TryEnrichWithBass(filePath, info);

        // Soft-fail defaults if we still have nothing
        if (info.DurationMs <= 0) info.DurationMs = 180000;
        if (info.SampleRate <= 0) info.SampleRate = 44100;
        if (info.Channels <= 0) info.Channels = 2;
        if (info.BitRate <= 0) info.BitRate = 320;

        return info;
    }

    /// <summary>Attempts to refine technical parameters via BASS decode stream.</summary>
    private void TryEnrichWithBass(string filePath, Track info)
    {
        try
        {
            int stream = Bass.CreateStream(filePath, 0, 0, BassFlags.Decode);
            if (stream == 0) return;

            try
            {
                if (Bass.ChannelGetInfo(stream, out ChannelInfo channelInfo))
                {
                    if (channelInfo.Frequency > 0)
                        info.SampleRate = channelInfo.Frequency;
                    if (channelInfo.Channels > 0)
                        info.Channels = channelInfo.Channels;
                }

                double seconds = Bass.ChannelBytes2Seconds(stream, Bass.ChannelGetLength(stream));
                if (seconds > 0)
                    info.DurationMs = (long)(seconds * 1000);

                if (Bass.ChannelGetAttribute(stream, ChannelAttribute.Bitrate, out float bitrate) && bitrate > 0)
                    info.BitRate = (int)bitrate;
            }
            finally
            {
                Bass.StreamFree(stream);
            }
        }
        catch
        {
            // BASS native DLLs may be absent; TagLibSharp values remain.
        }
    }

    /// <summary>Extracts the embedded cover art bytes, or null if none.</summary>
    public byte[]? ReadCoverArt(string filePath)
    {
        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            var pictures = tagFile.Tag.Pictures;
            if (pictures == null || pictures.Length == 0) return null;

            // Prefer front cover
            var cover = pictures.FirstOrDefault(p => p.Type == PictureType.FrontCover)
                        ?? pictures[0];
            return cover.Data?.Data;
        }
        catch
        {
            return null;
        }
    }
}
