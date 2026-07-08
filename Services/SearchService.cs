using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Dapper;
using MediaMusic.Data;
using MediaMusic.Data.Models;
using MediaMusic.Data.Repositories;

namespace MediaMusic.Services;

/// <summary>
/// Represents a search suggestion returned by the autocomplete system.
/// </summary>
public sealed class SearchSuggestion
{
    /// <summary>Gets or sets the suggestion text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the category of the suggestion (e.g. Track, Album, Artist).</summary>
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Provides unified search across tracks, albums, artists, and genres, 
/// autocomplete suggestions with debouncing, and search history management (PRD §2.2).
/// </summary>
public sealed class SearchService : IDisposable
{
    private readonly TrackRepository _trackRepo;
    private readonly AlbumRepository _albumRepo;
    private readonly ArtistRepository _artistRepo;
    private readonly GenreRepository _genreRepo;
    private readonly DbConnectionFactory _dbFactory;
    private readonly ILogger<SearchService> _logger;

    private readonly SemaphoreSlim _suggestionLock = new(1, 1);
    private CancellationTokenSource? _suggestionCts;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchService"/> class.
    /// </summary>
    public SearchService(
        TrackRepository trackRepo,
        AlbumRepository albumRepo,
        ArtistRepository artistRepo,
        GenreRepository genreRepo,
        DbConnectionFactory dbFactory,
        ILogger<SearchService> logger)
    {
        _trackRepo = trackRepo;
        _albumRepo = albumRepo;
        _artistRepo = artistRepo;
        _genreRepo = genreRepo;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>Performs a unified search across tracks, albums, artists, and genres with relevance ranking.</summary>
    public async Task<SearchResult> SearchAllAsync(string searchTerm, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new SearchResult();
        }

        var cleanedTerm = searchTerm.Trim();

        try
        {
            // Execute all repository searches concurrently
            var tracksTask = _trackRepo.SearchAsync(null, null, null, cleanedTerm);
            var albumsTask = _albumRepo.SearchAsync(cleanedTerm, ct);
            var artistsTask = _artistRepo.SearchAsync(cleanedTerm, ct);
            var genresTask = _genreRepo.GetAllAsync(ct);

            await Task.WhenAll(tracksTask, albumsTask, artistsTask, genresTask);

            // Fetch results
            var rawTracks = tracksTask.Result ?? Enumerable.Empty<Track>();
            var rawAlbums = albumsTask.Result ?? Enumerable.Empty<Album>();
            var rawArtists = artistsTask.Result ?? Enumerable.Empty<Artist>();
            var rawGenres = genresTask.Result ?? Enumerable.Empty<Genre>();

            // Apply relevance ranking
            var rankedTracks = ApplyRelevanceRanking(rawTracks, cleanedTerm, t => t.Title ?? string.Empty)
                .Take(20)
                .ToList();

            var rankedAlbums = ApplyRelevanceRanking(rawAlbums, cleanedTerm, a => a.Title)
                .Take(20)
                .ToList();

            var rankedArtists = ApplyRelevanceRanking(rawArtists, cleanedTerm, a => a.Name)
                .Take(20)
                .ToList();

            var rankedGenres = rawGenres
                .Where(g => g.Name.Contains(cleanedTerm, StringComparison.OrdinalIgnoreCase))
                .Take(20)
                .ToList();

            // Fire-and-forget save search query to history
            _ = SaveSearchAsync(cleanedTerm, CancellationToken.None);

            return new SearchResult
            {
                Tracks = rankedTracks,
                Albums = rankedAlbums,
                Artists = rankedArtists,
                Genres = rankedGenres
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform global search for term: '{Term}'", cleanedTerm);
            return new SearchResult();
        }
    }

    /// <summary>Retrieves autocomplete suggestions matching a partial term with 300ms debouncing.</summary>
    public async Task<IEnumerable<SearchSuggestion>> GetSuggestionsAsync(string partialTerm, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(partialTerm) || partialTerm.Trim().Length < 2)
        {
            return Array.Empty<SearchSuggestion>();
        }

        var cleanedTerm = partialTerm.Trim();

        // Handle debouncing: cancel previous running suggestion request
        _suggestionCts?.Cancel();
        _suggestionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var linkedCt = _suggestionCts.Token;

        try
        {
            await Task.Delay(300, linkedCt);
        }
        catch (TaskCanceledException)
        {
            return Array.Empty<SearchSuggestion>();
        }

        await _suggestionLock.WaitAsync(linkedCt);
        try
        {
            linkedCt.ThrowIfCancellationRequested();

            const string sql = @"
                SELECT * FROM (
                    SELECT Title AS Text, 'Track' AS Category FROM Tracks WHERE Title LIKE @term LIMIT 5
                )
                UNION ALL
                SELECT * FROM (
                    SELECT Title AS Text, 'Album' AS Category FROM Albums WHERE Title LIKE @term LIMIT 3
                )
                UNION ALL
                SELECT * FROM (
                    SELECT Name AS Text, 'Artist' AS Category FROM Artists WHERE Name LIKE @term LIMIT 2
                )";

            var parameters = new { term = $"%{cleanedTerm}%" };

            return await ExecuteWithRetryAsync(async (conn) =>
            {
                return await conn.QueryAsync<SearchSuggestion>(new CommandDefinition(sql, parameters, cancellationToken: linkedCt));
            }, sql, parameters) ?? Array.Empty<SearchSuggestion>();
        }
        catch (OperationCanceledException)
        {
            return Array.Empty<SearchSuggestion>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching suggestions for partial term '{Term}'", cleanedTerm);
            return Array.Empty<SearchSuggestion>();
        }
        finally
        {
            _suggestionLock.Release();
        }
    }

    /// <summary>Saves a search query to history and caps the history table at 100 entries.</summary>
    public async Task SaveSearchAsync(string searchTerm, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return;

        const string insertSql = "INSERT INTO SearchHistory (SearchTerm, SearchedAt) VALUES (@searchTerm, datetime('now'))";
        const string capSql = "DELETE FROM SearchHistory WHERE Id NOT IN (SELECT Id FROM SearchHistory ORDER BY SearchedAt DESC LIMIT 100)";
        
        var parameters = new { searchTerm = searchTerm.Trim() };

        try
        {
            await ExecuteWithRetryAsync(async (conn) =>
            {
                await conn.ExecuteAsync(new CommandDefinition(insertSql, parameters, cancellationToken: ct));
                await conn.ExecuteAsync(new CommandDefinition(capSql, cancellationToken: ct));
                return 0;
            }, insertSql, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save search term '{Term}' to history.", searchTerm);
        }
    }

    /// <summary>Gets the recent 10 unique search queries ordered by timestamp descending.</summary>
    public async Task<IEnumerable<string>> GetRecentSearchesAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT SearchTerm FROM SearchHistory GROUP BY SearchTerm ORDER BY MAX(SearchedAt) DESC LIMIT 10";
        try
        {
            var results = await ExecuteWithRetryAsync(async (conn) =>
            {
                return await conn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct));
            }, sql, null);
            return results ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve recent searches.");
            return Array.Empty<string>();
        }
    }

    /// <summary>Clears the entire search history.</summary>
    public async Task ClearHistoryAsync(CancellationToken ct = default)
    {
        const string sql = "DELETE FROM SearchHistory";
        try
        {
            await ExecuteWithRetryAsync(async (conn) =>
            {
                return await conn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
            }, sql, null);
            _logger.LogInformation("Search history successfully cleared.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear search history.");
        }
    }

    private IEnumerable<T> ApplyRelevanceRanking<T>(IEnumerable<T> items, string term, Func<T, string> textSelector)
    {
        return items.Select(item =>
        {
            var text = textSelector(item);
            int score = 0;

            if (string.Equals(text, term, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
            else if (text.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 50;
            }
            else if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 25;
            }

            // Track-specific boosting
            if (item is Track track)
            {
                score += (int)Math.Log10(track.PlayCount + 1) * 5;

                if (DateTime.TryParse(track.LastPlayed, out var lastPlayed) && (DateTime.Now - lastPlayed).TotalDays <= 7)
                {
                    score += 10;
                }
            }

            return new { Item = item, Score = score };
        })
        .OrderByDescending(x => x.Score)
        .Select(x => x.Item);
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<SqliteConnection, Task<T>> operation, string query, object? parameters)
    {
        try
        {
            using var conn = _dbFactory.Create();
            return await operation(conn);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 5) // SQLITE_BUSY
        {
            _logger.LogWarning("Database busy. Retrying operation after 100ms. Query: {Query}", query);
            await Task.Delay(100);
            try
            {
                using var conn = _dbFactory.Create();
                return await operation(conn);
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx, "Database operation failed on retry. Query: {Query}, Params: {@Params}", query, parameters);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database operation encountered an exception. Query: {Query}, Params: {@Params}", query, parameters);
            throw;
        }
    }

    /// <summary>
    /// Disposes the search service resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _suggestionCts?.Dispose();
        _suggestionLock.Dispose();
        _disposed = true;
    }
}
