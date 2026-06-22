using System.Globalization;
using System.Text.RegularExpressions;

namespace MediaMusic.Lyrics;

/// <summary>
/// Parses LRC lyrics files into a time-sorted list of <see cref="LyricsLine"/>.
/// Supports millisecond-precision timestamps (e.g. <c>[01:23.456]</c>) and the
/// compact multi-tag form <c>[00:01.00][00:05.00]line</c>.
/// </summary>
public sealed class LrcParser
{
    // Matches [mm:ss.xx] or [mm:ss.xxx] (and the legacy [mm:ss] form).
    private static readonly Regex TagRegex =
        new(@"\[(\d{1,3}):(\d{2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);

    /// <summary>Parses raw LRC text. Returns an empty list if no tags are found.</summary>
    public IReadOnlyList<LyricsLine> Parse(string lrcText)
    {
        if (string.IsNullOrWhiteSpace(lrcText))
            return Array.Empty<LyricsLine>();

        var lines = new List<LyricsLine>();

        foreach (var rawLine in lrcText.Split('\n', '\r'))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            var matches = TagRegex.Matches(rawLine);
            if (matches.Count == 0)
                continue;

            // Text follows the last timestamp tag on the line.
            var start = matches[^1].Index + matches[^1].Length;
            var text = rawLine[start..].Trim();

            foreach (Match m in matches)
            {
                var minutes = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                var seconds = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                var fracText = m.Groups[3].Value;
                var milliseconds = ParseFraction(fracText);
                var time = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds)
                           + TimeSpan.FromMilliseconds(milliseconds);
                lines.Add(new LyricsLine(time, text));
            }
        }

        lines.Sort((a, b) => a.Time.CompareTo(b.Time));
        return lines;
    }

    private static int ParseFraction(string fracText)
    {
        if (string.IsNullOrEmpty(fracText))
            return 0;

        // Normalize 2-digit (centiseconds) and 3-digit (ms) forms to milliseconds.
        return fracText.Length switch
        {
            2 => int.Parse(fracText, CultureInfo.InvariantCulture) * 10,
            3 => int.Parse(fracText, CultureInfo.InvariantCulture),
            _ => (int)Math.Round(int.Parse(fracText, CultureInfo.InvariantCulture)
                * Math.Pow(10, 3 - fracText.Length))
        };
    }
}
