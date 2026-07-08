using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Dapper;
using MediaMusic.Data.Models;

namespace MediaMusic.Data.Repositories;

/// <summary>
/// Dapper-backed repository for <see cref="Album"/> entities.
/// </summary>
public sealed class AlbumRepository
{
    private readonly DbConnectionFactory _factory;
    private readonly ILogger<AlbumRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlbumRepository"/> class.
    /// </summary>
    public AlbumRepository(DbConnectionFactory factory, ILogger<AlbumRepository> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <summary>Gets an album by its ID.</summary>
    public async Task<Album?> GetByIdAsync(long id)
    {
        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.QueryFirstOrDefaultAsync<Album>(
                "SELECT * FROM Albums WHERE Id = @id", new { id });
        }, "SELECT * FROM Albums WHERE Id = @id", new { id });
    }

    /// <summary>Gets all albums ordered by title, limited to 100.</summary>
    public async Task<IEnumerable<Album>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM Albums ORDER BY Title ASC LIMIT 100";
        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.QueryAsync<Album>(new CommandDefinition(sql, cancellationToken: ct));
        }, sql, null) ?? Array.Empty<Album>();
    }

    /// <summary>Searches albums by title (exact match first).</summary>
    public async Task<IEnumerable<Album>> SearchAsync(string searchTerm, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetAllAsync(ct);

        const string sql = @"
            SELECT * FROM Albums 
            WHERE NormalizedTitle LIKE @term 
            ORDER BY 
              CASE WHEN NormalizedTitle = @exactTerm THEN 0 ELSE 1 END,
              Title ASC
            LIMIT 100";

        var exactTerm = searchTerm.Trim().ToLowerInvariant();
        var term = $"%{exactTerm}%";
        var parameters = new { term, exactTerm };

        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.QueryAsync<Album>(new CommandDefinition(sql, parameters, cancellationToken: ct));
        }, sql, parameters) ?? Array.Empty<Album>();
    }

    /// <summary>Gets albums by artist ID ordered by year descending, then title.</summary>
    public async Task<IEnumerable<Album>> GetByArtistAsync(long artistId, CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM Albums WHERE ArtistId = @artistId ORDER BY Year DESC, Title ASC";
        var parameters = new { artistId };

        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.QueryAsync<Album>(new CommandDefinition(sql, parameters, cancellationToken: ct));
        }, sql, parameters) ?? Array.Empty<Album>();
    }

    /// <summary>Gets all tracks in an album ordered by track number, then title.</summary>
    public async Task<IEnumerable<Track>> GetTracksAsync(long albumId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT t.*, ar.Name AS ArtistName, al.Title AS AlbumTitle, g.Name AS GenreName
            FROM Tracks t
            LEFT JOIN Artists ar ON t.ArtistId = ar.Id
            LEFT JOIN Albums al ON t.AlbumId = al.Id
            LEFT JOIN Genres g ON t.GenreId = g.Id
            WHERE t.AlbumId = @albumId
            ORDER BY t.TrackNo ASC, t.Title ASC";

        var parameters = new { albumId };

        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.QueryAsync<Track>(new CommandDefinition(sql, parameters, cancellationToken: ct));
        }, sql, parameters) ?? Array.Empty<Track>();
    }

    /// <summary>Inserts or updates an album.</summary>
    public async Task<long> UpsertAsync(Album album)
    {
        return await ExecuteWithRetryAsync(async (conn) =>
        {
            var existingId = await conn.QueryFirstOrDefaultAsync<long?>(
                "SELECT Id FROM Albums WHERE NormalizedTitle = @NormalizedTitle AND (ArtistId = @ArtistId OR (ArtistId IS NULL AND @ArtistId IS NULL))", album);
            if (existingId.HasValue)
            {
                album.Id = existingId.Value;
                await conn.ExecuteAsync(
                    "UPDATE Albums SET Year = COALESCE(@Year, Year), CoverPath = COALESCE(@CoverPath, CoverPath) WHERE Id = @Id", album);
                return existingId.Value;
            }
            else
            {
                return await conn.ExecuteScalarAsync<long>(
                    "INSERT INTO Albums (Title, NormalizedTitle, ArtistId, Year, CoverPath) " +
                    "VALUES (@Title, @NormalizedTitle, @ArtistId, @Year, @CoverPath) " +
                    "RETURNING Id", album);
            }
        }, "UpsertAsync", album);
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<SqliteConnection, Task<T>> operation, string query, object? parameters)
    {
        try
        {
            using var conn = _factory.Create();
            return await operation(conn);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 5) // SQLITE_BUSY
        {
            _logger.LogWarning("Database busy. Retrying operation after 100ms. Query: {Query}", query);
            await Task.Delay(100);
            try
            {
                using var conn = _factory.Create();
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
