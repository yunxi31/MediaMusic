using MediaMusic.Audio;

namespace MediaMusic.Lyrics;

/// <summary>
/// Matches a <c>.lrc</c> file to the currently playing track and tracks which
/// line is active so the UI (main scroll view + desktop overlay) can highlight it.
/// </summary>
public sealed class LyricsService
{
    private readonly LrcParser _parser;
    private readonly PlayerService _player;
    private readonly ILogger<LyricsService> _logger;
    private IReadOnlyList<LyricsLine> _lines = Array.Empty<LyricsLine>();

    public LyricsService(LrcParser parser, PlayerService player, ILogger<LyricsService> logger)
    {
        _parser = parser;
        _player = player;
        _logger = logger;
    }

    /// <summary>The currently loaded lyrics lines, sorted by time.</summary>
    public IReadOnlyList<LyricsLine> Lines => _lines;

    /// <summary>Loads lyrics for the given audio file (same dir, same name, .lrc extension).</summary>
    public void LoadForTrack(string audioFilePath)
    {
        if (string.IsNullOrEmpty(audioFilePath))
        {
            _lines = Array.Empty<LyricsLine>();
            return;
        }

        var lrcPath = Path.ChangeExtension(audioFilePath, ".lrc");
        if (!File.Exists(lrcPath))
        {
            lrcPath = Path.ChangeExtension(audioFilePath, ".LRC");
        }

        if (!File.Exists(lrcPath))
        {
            _lines = Array.Empty<LyricsLine>();
            return;
        }

        try
        {
            var lrcText = ReadTextWithEncoding(lrcPath);
            _lines = _parser.Parse(lrcText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load lyrics from {Path}.", lrcPath);
            _lines = Array.Empty<LyricsLine>();
        }
    }

    private static string ReadTextWithEncoding(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        
        // Check for BOM (Byte Order Mark)
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return System.Text.Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return System.Text.Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        // Check if valid UTF-8
        if (IsValidUtf8(bytes))
        {
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        // Fallback to GB18030 / GBK (Traditional/Simplified Chinese Windows ANSI)
        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var gbk = System.Text.Encoding.GetEncoding("GB18030");
            return gbk.GetString(bytes);
        }
        catch
        {
            return System.Text.Encoding.Default.GetString(bytes);
        }
    }

    private static bool IsValidUtf8(byte[] bytes)
    {
        int i = 0;
        while (i < bytes.Length)
        {
            if (bytes[i] <= 0x7F)
            {
                i++;
                continue;
            }
            
            int len;
            if ((bytes[i] & 0xE0) == 0xC0) len = 2;
            else if ((bytes[i] & 0xF0) == 0xE0) len = 3;
            else if ((bytes[i] & 0xF8) == 0xF0) len = 4;
            else return false;

            if (i + len > bytes.Length) return false;

            for (int j = 1; j < len; j++)
            {
                if ((bytes[i + j] & 0xC0) != 0x80) return false;
            }
            i += len;
        }
        return true;
    }

    /// <summary>Returns the index of the lyrics line active at the given position, or -1.</summary>
    public int GetActiveIndex(TimeSpan position)
    {
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
        if (idx < 0) return false;

        var position = TimeSpan.FromMilliseconds(_player.State.PositionMs);
        return idx == GetActiveIndex(position);
    }
}
