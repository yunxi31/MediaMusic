using MediaMusic.Audio;
using MediaMusic.Data.Models;

namespace MediaMusic.Library;

/// <summary>
/// Lightweight drag-and-drop mode (PRD §2.2). Accepts external files/folders
/// dropped onto the playlist area and enqueues them for immediate playback
/// WITHOUT writing anything to the SQLite library.
/// </summary>
public sealed class DragDropHandler
{
    private readonly PlayerService _player;
    private readonly MetadataReader _metadataReader;
    private readonly ILogger<DragDropHandler> _logger;

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".flac", ".ape", ".wav", ".mp3", ".aac", ".m4a", ".ogg" };

    public DragDropHandler(PlayerService player, MetadataReader metadataReader, ILogger<DragDropHandler> logger)
    {
        _player = player;
        _metadataReader = metadataReader;
        _logger = logger;
    }

    /// <summary>
    /// Resolves dropped paths (files and/or folders) into tracks and enqueues them.
    /// Returns the resolved track list so the UI can render it instantly.
    /// </summary>
    public IReadOnlyList<Track> HandleDrop(IEnumerable<string> paths, bool autoPlay)
    {
        var tracks = new List<Track>();
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                             .Where(f => SupportedExtensions.Contains(Path.GetExtension(f))))
                {
                    tracks.Add(_metadataReader.Read(file));
                }
            }
            else if (File.Exists(path) && SupportedExtensions.Contains(Path.GetExtension(path)))
            {
                tracks.Add(_metadataReader.Read(path));
            }
        }

        // TODO: push tracks into the PlayerService queue; honor autoPlay flag.
        if (autoPlay && tracks.Count > 0)
            _player.Play(tracks[0]);

        _logger.LogInformation("Drag-drop resolved {Count} tracks.", tracks.Count);
        return tracks;
    }
}
