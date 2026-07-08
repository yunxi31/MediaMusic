using System;
using System.Collections.Generic;

namespace MediaMusic.Data.Models;

/// <summary>
/// Time range for play history queries.
/// </summary>
public enum TimeRange
{
    /// <summary>
    /// Last 7 days.
    /// </summary>
    Last7Days,

    /// <summary>
    /// Last 30 days.
    /// </summary>
    Last30Days,

    /// <summary>
    /// Last year.
    /// </summary>
    LastYear,

    /// <summary>
    /// All time.
    /// </summary>
    AllTime
}

/// <summary>
/// Playback statistics for a given time range.
/// </summary>
public sealed class PlayStatistics
{
    /// <summary>
    /// Gets the total play count.
    /// </summary>
    public int TotalPlayCount { get; init; }

    /// <summary>
    /// Gets the total play time in milliseconds.
    /// </summary>
    public long TotalPlayTimeMs { get; init; }

    /// <summary>
    /// Gets the mapping of top genres to their play counts.
    /// </summary>
    public Dictionary<string, int> TopGenres { get; init; } = new();

    /// <summary>
    /// Gets the total play time as a TimeSpan.
    /// </summary>
    public TimeSpan TotalPlayTime => TimeSpan.FromMilliseconds(TotalPlayTimeMs);
}
