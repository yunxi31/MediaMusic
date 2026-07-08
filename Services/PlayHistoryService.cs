using System;
using System.Collections.Concurrent;
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
/// Provides tracking and retrieval of track playback history, 60-second duplicate filtering,
/// and aggregates listening statistics (PRD §2.4).
/// </summary>
public sealed class PlayHistoryService
{
    private readonly DbConnectionFactory _dbFactory;
    private readonly TrackRepository _trackRepo;
    private readonly ILogger<PlayHistoryService> _logger;
    private readonly ConcurrentDictionary<long, DateTime> _recentPlays = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayHistoryService"/> class.
    /// </summary>
    public PlayHistoryService(
        DbConnectionFactory dbFactory,
        TrackRepository trackRepo,
        ILogger<PlayHistoryService> logger)
    {
        _dbFactory = dbFactory;
        _trackRepo = trackRepo;
        _logger = logger;
    }

    /// <summary>Records a track playback event with 60-second duplication filtering.</summary>
    public async Task RecordPlayAsync(long trackId, CancellationToken ct = default)
    {
        if (trackId <= 0)
            return;

        var now = DateTime.UtcNow;
        if (_recentPlays.TryGetValue(trackId, out var lastPlayTime))
        {
            if ((now - lastPlayTime).TotalSeconds < 60)
            {
                _logger.LogDebug("Duplicate playback of track ID {Id} detected within 60 seconds. Skipping record.", trackId);
                return;
            }
        }

        _recentPlays[trackId] = now;
        CleanupCache();

        const string insertSql = "INSERT INTO PlayHistory (TrackId, PlayedAt) VALUES (@trackId, datetime('now'))";

        try
        {
            // Execute DB insertions
            await ExecuteWithRetryAsync(async (conn) =>
            {
                await conn.ExecuteAsync(new CommandDefinition(insertSql, new { trackId }, cancellationToken: ct));
                return 0;
            }, insertSql, new { trackId });

            // Increment play count in Tracks table
            await _trackRepo.IncrementPlayCountAsync(trackId);
            
            _logger.LogInformation("Playback recorded for track ID {Id}.", trackId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record playback history for track ID {Id}.", trackId);
        }
    }

    /// <summary>Gets the list of recently played tracks (deduplicated by track).</summary>
    public async Task<IEnumerable<Track>> GetRecentlyPlayedAsync(int limit = 50, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT t.*, ar.Name AS ArtistName, al.Title AS AlbumTitle, g.Name AS GenreName
            FROM Tracks t
            JOIN PlayHistory ph ON t.Id = ph.TrackId
            LEFT JOIN Artists ar ON t.ArtistId = ar.Id
            LEFT JOIN Albums al ON t.AlbumId = al.Id
            LEFT JOIN Genres g ON t.GenreId = g.Id
            WHERE ph.Id IN (
                SELECT MAX(Id) FROM PlayHistory GROUP BY TrackId
            )
            ORDER BY ph.PlayedAt DESC
            LIMIT @limit";

        var parameters = new { limit };

        try
        {
            var results = await ExecuteWithRetryAsync(async (conn) =>
            {
                return await conn.QueryAsync<Track>(new CommandDefinition(sql, parameters, cancellationToken: ct));
            }, sql, parameters);
            return results ?? Array.Empty<Track>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve recently played tracks.");
            return Array.Empty<Track>();
        }
    }

    /// <summary>Gets top played tracks within a specified time range.</summary>
    public async Task<IEnumerable<Track>> GetTopTracksAsync(TimeRange range, int limit = 20, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT t.*, ar.Name AS ArtistName, al.Title AS AlbumTitle, g.Name AS GenreName, COUNT(ph.Id) AS PlayCount
            FROM Tracks t
            JOIN PlayHistory ph ON t.Id = ph.TrackId
            LEFT JOIN Artists ar ON t.ArtistId = ar.Id
            LEFT JOIN Albums al ON t.AlbumId = al.Id
            LEFT JOIN Genres g ON t.GenreId = g.Id
            WHERE ph.PlayedAt >= @dateFilter
            GROUP BY t.Id
            ORDER BY PlayCount DESC, MAX(ph.PlayedAt) DESC
            LIMIT @limit";

        var dateFilter = GetDateFilter(range);
        var parameters = new { dateFilter, limit };

        try
        {
            var results = await ExecuteWithRetryAsync(async (conn) =>
            {
                return await conn.QueryAsync<Track>(new CommandDefinition(sql, parameters, cancellationToken: ct));
            }, sql, parameters);
            return results ?? Array.Empty<Track>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve top tracks.");
            return Array.Empty<Track>();
        }
    }

    /// <summary>Gets listening statistics for a given time range.</summary>
    public async Task<PlayStatistics> GetStatisticsAsync(TimeRange range, CancellationToken ct = default)
    {
        const string countSql = "SELECT COUNT(*) FROM PlayHistory WHERE PlayedAt >= @dateFilter";
        const string durationSql = @"
            SELECT SUM(t.DurationMs) 
            FROM PlayHistory ph
            JOIN Tracks t ON ph.TrackId = t.Id
            WHERE ph.PlayedAt >= @dateFilter";
        const string genreSql = @"
            SELECT g.Name AS GenreName, COUNT(ph.Id) AS PlayCount
            FROM PlayHistory ph
            JOIN Tracks t ON ph.TrackId = t.Id
            JOIN Genres g ON t.GenreId = g.Id
            WHERE ph.PlayedAt >= @dateFilter
            GROUP BY g.Id
            ORDER BY PlayCount DESC
            LIMIT 5";

        var dateFilter = GetDateFilter(range);
        var parameters = new { dateFilter };

        try
        {
            return await ExecuteWithRetryAsync(async (conn) =>
            {
                var totalPlayCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: ct));
                var totalPlayTimeMs = await conn.ExecuteScalarAsync<long?>(new CommandDefinition(durationSql, parameters, cancellationToken: ct)) ?? 0L;

                var genreRows = await conn.QueryAsync<(string GenreName, int PlayCount)>(new CommandDefinition(genreSql, parameters, cancellationToken: ct));
                var topGenres = genreRows.ToDictionary(r => r.GenreName, r => r.PlayCount);

                return new PlayStatistics
                {
                    TotalPlayCount = totalPlayCount,
                    TotalPlayTimeMs = totalPlayTimeMs,
                    TopGenres = topGenres
                };
            }, "GetStatisticsAsync", parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute playback statistics.");
            return new PlayStatistics();
        }
    }

    private string GetDateFilter(TimeRange range)
    {
        return range switch
        {
            TimeRange.Last7Days => DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd HH:mm:ss"),
            TimeRange.Last30Days => DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd HH:mm:ss"),
            TimeRange.LastYear => DateTime.UtcNow.AddYears(-1).ToString("yyyy-MM-dd HH:mm:ss"),
            TimeRange.AllTime => "1970-01-01 00:00:00",
            _ => "1970-01-01 00:00:00"
        };
    }

    private void CleanupCache()
    {
        var threshold = DateTime.UtcNow.AddMinutes(-2);
        foreach (var kvp in _recentPlays.ToList())
        {
            if (kvp.Value < threshold)
            {
                _recentPlays.TryRemove(kvp.Key, out _);
            }
        }
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
}
