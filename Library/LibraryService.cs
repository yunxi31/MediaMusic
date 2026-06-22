using System.Threading.Channels;
using MediaMusic.Data.Models;

namespace MediaMusic.Library;

/// <summary>
/// Facade over the two library modes (PRD §2.2):
/// <list type="bullet">
/// <item><term>Lightweight</term><description><see cref="DragDropHandler"/> — files/folders enqueued instantly, no DB write.</description></item>
/// <item><term>Deep scan</term><description><see cref="LibraryScanner"/> — async metadata extraction into SQLite for cross-search.</description></item>
/// </list>
/// </summary>
public sealed class LibraryService
{
    private readonly LibraryScanner _scanner;
    private readonly DragDropHandler _dragDropHandler;
    private readonly MetadataEditor _metadataEditor;
    private readonly ILogger<LibraryService> _logger;

    public LibraryService(
        LibraryScanner scanner,
        DragDropHandler dragDropHandler,
        MetadataEditor metadataEditor,
        ILogger<LibraryService> logger)
    {
        _scanner = scanner;
        _dragDropHandler = dragDropHandler;
        _metadataEditor = metadataEditor;
        _logger = logger;
    }

    /// <summary>Lightweight mode: drop and play, no persistence.</summary>
    public IReadOnlyList<Track> DropAndPlay(IEnumerable<string> paths, bool autoPlay = true)
        => _dragDropHandler.HandleDrop(paths, autoPlay);

    /// <summary>Deep scan mode: walks roots and persists metadata to SQLite.</summary>
    public Task ScanAsync(IEnumerable<string> roots, IProgress<ScanProgress> progress, CancellationToken ct = default)
        => _scanner.ScanAsync(roots, progress, ct);

    /// <summary>Edits a track's metadata (file + DB sync).</summary>
    public Task<Track> EditMetadataAsync(Track track, byte[]? coverBytes, string? coverImagePath)
        => _metadataEditor.SaveAsync(track, coverBytes, coverImagePath);
}
