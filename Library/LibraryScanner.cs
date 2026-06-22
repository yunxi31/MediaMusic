using System.Threading.Channels;
using MediaMusic.Data.Models;
using MediaMusic.Data.Repositories;

namespace MediaMusic.Library;

/// <summary>
/// Background deep-scan mode (PRD §2.2). Walks configured local directories
/// asynchronously, extracts metadata for each supported audio file, and reports
/// progress so the UI can show a live scan counter without blocking.
/// </summary>
public sealed class LibraryScanner
{
    private readonly MetadataReader _metadataReader;
    private readonly ArtistRepository _artistRepository;
    private readonly AlbumRepository _albumRepository;
    private readonly GenreRepository _genreRepository;
    private readonly TrackRepository _trackRepository;
    private readonly ILogger<LibraryScanner> _logger;

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".flac", ".ape", ".wav", ".mp3", ".aac", ".m4a", ".ogg" };

    public LibraryScanner(
        MetadataReader metadataReader,
        ArtistRepository artistRepository,
        AlbumRepository albumRepository,
        GenreRepository genreRepository,
        TrackRepository trackRepository,
        ILogger<LibraryScanner> logger)
    {
        _metadataReader = metadataReader;
        _artistRepository = artistRepository;
        _albumRepository = albumRepository;
        _genreRepository = genreRepository;
        _trackRepository = trackRepository;
        _logger = logger;
    }

    /// <summary>
    /// Scans the given root directories and yields discovered tracks through
    /// <paramref name="progress"/>. Designed to run on a background task.
    /// </summary>
    public async Task ScanAsync(
        IEnumerable<string> rootDirectories,
        IProgress<ScanProgress> progress,
        CancellationToken cancellationToken = default)
    {
        var totalFound = 0;
        var processed = 0;

        var filesToScan = new List<string>();
        foreach (var root in rootDirectories.Where(Directory.Exists))
        {
            filesToScan.AddRange(EnumerateAudioFiles(root));
        }
        totalFound = filesToScan.Count;

        foreach (var file in filesToScan)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new ScanProgress(processed, totalFound, file));

            try
            {
                var track = _metadataReader.Read(file);

                // Upsert Artist
                var artistName = track.ArtistName;
                if (string.IsNullOrWhiteSpace(artistName))
                {
                    artistName = "Unknown Artist";
                }
                var artistId = await _artistRepository.UpsertAsync(new Artist
                {
                    Name = artistName,
                    NormalizedName = artistName.Trim().ToLowerInvariant()
                });
                track.ArtistId = artistId;

                // Upsert Album
                var albumTitle = track.AlbumTitle;
                if (string.IsNullOrWhiteSpace(albumTitle))
                {
                    albumTitle = "Unknown Album";
                }
                var albumId = await _albumRepository.UpsertAsync(new Album
                {
                    Title = albumTitle,
                    NormalizedTitle = albumTitle.Trim().ToLowerInvariant(),
                    ArtistId = artistId,
                    Year = track.Year
                });
                track.AlbumId = albumId;

                // Upsert Genre
                var genreName = track.GenreName;
                if (string.IsNullOrWhiteSpace(genreName))
                {
                    genreName = "Unknown";
                }
                var genreId = await _genreRepository.UpsertAsync(genreName);
                track.GenreId = genreId;

                // Upsert Track
                await _trackRepository.UpsertAsync(track);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to scan and save file {File}", file);
            }

            processed++;
            progress.Report(new ScanProgress(processed, totalFound, file));
        }

        _logger.LogInformation("Scan complete: {Processed}/{Total} files.", processed, totalFound);
    }

    private static IEnumerable<string> EnumerateAudioFiles(string root)
    {
        return Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)));
    }
}

/// <summary>Progress payload reported by <see cref="LibraryScanner"/>.</summary>
public sealed record ScanProgress(int Processed, int TotalFound, string CurrentFile);
