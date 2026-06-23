using MediaMusic.Data.Models;
using MediaMusic.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace MediaMusic.Library;

/// <summary>
/// Writes user-edited metadata back to the audio file header AND syncs the change
/// to the SQLite library (PRD §2.2). Supports replacing the embedded cover with a
/// user-supplied image.
/// </summary>
public sealed class MetadataEditor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetadataEditor> _logger;

    public MetadataEditor(IServiceProvider serviceProvider, ILogger<MetadataEditor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Persists edited fields (title/artist/album) and optional new cover image
    /// to the file, then returns an updated <see cref="Track"/> for DB upsert.
    /// </summary>
    public async Task<Track> SaveAsync(Track track, byte[]? coverBytes, string? coverImagePath)
    {
        _logger.LogInformation("SaveAsync called for file: {Path}", track.FilePath);

        // 1. Write tags back to the physical audio file using TagLib
        try
        {
            using (var file = TagLib.File.Create(track.FilePath))
            {
                file.Tag.Title = track.Title;
                file.Tag.Performers = string.IsNullOrEmpty(track.ArtistName) ? Array.Empty<string>() : new[] { track.ArtistName };
                file.Tag.Album = track.AlbumTitle;
                file.Tag.Genres = string.IsNullOrEmpty(track.GenreName) ? Array.Empty<string>() : new[] { track.GenreName };
                
                if (track.Year.HasValue)
                {
                    file.Tag.Year = (uint)track.Year.Value;
                }
                else
                {
                    file.Tag.Year = 0;
                }

                if (track.TrackNo.HasValue)
                {
                    file.Tag.Track = (uint)track.TrackNo.Value;
                }
                else
                {
                    file.Tag.Track = 0;
                }

                if (track.TotalTracks.HasValue)
                {
                    file.Tag.TrackCount = (uint)track.TotalTracks.Value;
                }
                else
                {
                    file.Tag.TrackCount = 0;
                }

                if (coverBytes != null && coverBytes.Length > 0)
                {
                    var pic = new TagLib.Picture(new TagLib.ByteVector(coverBytes))
                    {
                        Type = TagLib.PictureType.FrontCover,
                        MimeType = "image/jpeg",
                        Description = "Cover"
                    };
                    file.Tag.Pictures = new TagLib.IPicture[] { pic };
                }

                file.Save();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save audio file tags for {Path}", track.FilePath);
        }

        // 2. Sync changes to the SQLite database
        using (var scope = _serviceProvider.CreateScope())
        {
            var artistRepository = scope.ServiceProvider.GetRequiredService<ArtistRepository>();
            var albumRepository = scope.ServiceProvider.GetRequiredService<AlbumRepository>();
            var genreRepository = scope.ServiceProvider.GetRequiredService<GenreRepository>();
            var trackRepository = scope.ServiceProvider.GetRequiredService<TrackRepository>();

            // Upsert Artist
            var artistName = track.ArtistName ?? "Unknown Artist";
            var artistId = await artistRepository.UpsertAsync(new Artist
            {
                Name = artistName,
                NormalizedName = artistName.Trim().ToLowerInvariant()
            });
            track.ArtistId = artistId;

            // Upsert Album
            var albumTitle = track.AlbumTitle ?? "Unknown Album";
            var albumId = await albumRepository.UpsertAsync(new Album
            {
                Title = albumTitle,
                NormalizedTitle = albumTitle.Trim().ToLowerInvariant(),
                ArtistId = artistId,
                Year = track.Year
            });
            track.AlbumId = albumId;

            // Upsert Genre
            var genreName = track.GenreName ?? "Unknown";
            var genreId = await genreRepository.UpsertAsync(genreName);
            track.GenreId = genreId;

            // Save new cover art if provided
            if (coverBytes != null && coverBytes.Length > 0)
            {
                try
                {
                    var hash = SHA256.HashData(coverBytes);
                    var hashStr = Convert.ToHexStringLower(hash)[..16];
                    var fileName = $"{hashStr}.jpg";
                    var coverDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "covers");
                    Directory.CreateDirectory(coverDir);
                    var fullPath = Path.Combine(coverDir, fileName);
                    if (!File.Exists(fullPath))
                    {
                        await File.WriteAllBytesAsync(fullPath, coverBytes);
                    }
                    track.CoverPath = $"/covers/{fileName}";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save cover art file");
                }
            }
            else if (!string.IsNullOrEmpty(coverImagePath))
            {
                track.CoverPath = coverImagePath;
            }

            // Upsert Track to update database fields
            await trackRepository.UpsertAsync(track);
        }

        return track;
    }
}
