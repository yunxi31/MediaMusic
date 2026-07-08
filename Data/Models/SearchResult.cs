using System;
using System.Collections.Generic;

namespace MediaMusic.Data.Models;

/// <summary>
/// Unified search result containing tracks, albums, artists, and genres.
/// </summary>
public sealed class SearchResult
{
    /// <summary>
    /// Gets the list of matching tracks.
    /// </summary>
    public IReadOnlyList<Track> Tracks { get; init; } = Array.Empty<Track>();

    /// <summary>
    /// Gets the list of matching albums.
    /// </summary>
    public IReadOnlyList<Album> Albums { get; init; } = Array.Empty<Album>();

    /// <summary>
    /// Gets the list of matching artists.
    /// </summary>
    public IReadOnlyList<Artist> Artists { get; init; } = Array.Empty<Artist>();

    /// <summary>
    /// Gets the list of matching genres.
    /// </summary>
    public IReadOnlyList<Genre> Genres { get; init; } = Array.Empty<Genre>();

    /// <summary>
    /// Gets the total number of results across all categories.
    /// </summary>
    public int TotalResults => Tracks.Count + Albums.Count + Artists.Count + Genres.Count;
}
