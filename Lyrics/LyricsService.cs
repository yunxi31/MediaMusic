using MediaMusic.Audio;

namespace MediaMusic.Lyrics;

/// <summary>
/// Matches a <c>.lrc</c> file to the currently playing track and tracks which
/// line is active so the UI (main scroll view + desktop overlay) can highlight it.
/// </summary>
public sealed class LyricsService
{
    private readonly LrcParser _parser;
    private readonly ILogger<LyricsService> _logger;
    private IReadOnlyList<LyricsLine> _lines = Array.Empty<LyricsLine>();

    public LyricsService(LrcParser parser, ILogger<LyricsService> logger)
    {
        _parser = parser;
        _logger = logger;
    }

    /// <summary>The currently loaded lyrics lines, sorted by time.</summary>
    public IReadOnlyList<LyricsLine> Lines => _lines;

    /// <summary>Loads lyrics for the given audio file (same dir, same name, .lrc extension).</summary>
    public void LoadForTrack(string audioFilePath)
    {
        // TODO: probe same-name .lrc (and .LRC) next to the audio file; fall back to
        //       embedded lyrics tag from MetadataReader. Parse into _lines.
        var lrcPath = Path.ChangeExtension(audioFilePath, ".lrc");
        if (!File.Exists(lrcPath))
        {
            _lines = Array.Empty<LyricsLine>();
            return;
        }

        try
        {
            _lines = _parser.Parse(File.ReadAllText(lrcPath));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load lyrics from {Path}.", lrcPath);
            _lines = Array.Empty<LyricsLine>();
        }
    }

    /// <summary>Returns the index of the lyrics line active at the given position, or -1.</summary>
    public int GetActiveIndex(TimeSpan position)
    {
        // TODO: binary search _lines for the last line with Time <= position.
        for (var i = _lines.Count - 1; i >= 0; i--)
        {
            if (_lines[i].Time <= position)
                return i;
        }
        return -1;
    }

    /// <summary>Checks if the given line is the currently active lyrics line.</summary>
    public bool IsActive(LyricsLine line)
    {
        var idx = ((IList<LyricsLine>)_lines).IndexOf(line);
        return idx >= 0 && idx == GetActiveIndex(TimeSpan.Zero);
    }
}
