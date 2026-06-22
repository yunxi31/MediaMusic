using MediaMusic.Data.Models;

namespace MediaMusic.Library;

/// <summary>
/// Writes user-edited metadata back to the audio file header AND syncs the change
/// to the SQLite library (PRD §2.2). Supports replacing the embedded cover with a
/// user-supplied image.
/// </summary>
public sealed class MetadataEditor
{
    private readonly ILogger<MetadataEditor> _logger;

    public MetadataEditor(ILogger<MetadataEditor> logger) => _logger = logger;

    /// <summary>
    /// Persists edited fields (title/artist/album) and optional new cover image
    /// to the file, then returns an updated <see cref="Track"/> for DB upsert.
    /// </summary>
    public Task<Track> SaveAsync(Track track, byte[]? coverBytes, string? coverImagePath)
    {
        // TODO: 1. open file with ATL.NET, set Tag.Title/Artist/Album + PictureCollection,
        //          call Save(). 2. upsert Track row via repository.
        _logger.LogDebug("SaveAsync stub for {Path}.", track.FilePath);
        return Task.FromResult(track);
    }
}
