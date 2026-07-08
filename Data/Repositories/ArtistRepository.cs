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
/// Dapper-backed repository for <see cref="Artist"/> entities.
/// </summary>
public sealed class ArtistRepository
{
    private readonly DbConnectionFactory _factory;
    private readonly ILogger<ArtistRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtistRepository"/> class.
    /// </summary>
    public ArtistRepository(DbConnectionFactory factory, ILogger<ArtistRepository> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <summary>Gets an artist by ID.</summary>
    public async Task<Artist?> GetByIdAsync(long id)
    {
        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.QueryFirstOrDefaultAsync<Artist>(
                "SELECT * FROM Artists WHERE Id = @id", new { id });
        }, "SELECT * FROM Artists WHERE Id = @id", new { id });
    }

    /// <summary>Gets all artists ordered by name, limited to 100.</summary>
    public async Task<IEnumerable<Artist>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM Artists ORDER BY Name ASC LIMIT 100";
        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.QueryAsync<Artist>(new CommandDefinition(sql, cancellationToken: ct));
        }, sql, null) ?? Array.Empty<Artist>();
    }

    /// <summary>Searches artists by name (exact match first).</summary>
    public async Task<IEnumerable<Artist>> SearchAsync(string searchTerm, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetAllAsync(ct);

        const string sql = @"
            SELECT * FROM Artists 
            WHERE NormalizedName LIKE @term 
            ORDER BY 
              CASE WHEN NormalizedName = @exactTerm THEN 0 ELSE 1 END,
              Name ASC
            LIMIT 100";

        var exactTerm = searchTerm.Trim().ToLowerInvariant();
        var term = $"%{exactTerm}%";
        var parameters = new { term, exactTerm };

        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.QueryAsync<Artist>(new CommandDefinition(sql, parameters, cancellationToken: ct));
        }, sql, parameters) ?? Array.Empty<Artist>();
    }

    /// <summary>Gets albums by artist ID ordered by year descending, then title.</summary>
    public async Task<IEnumerable<Album>> GetAlbumsAsync(long artistId, CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM Albums WHERE ArtistId = @artistId ORDER BY Year DESC, Title ASC";
        var parameters = new { artistId };

        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.QueryAsync<Album>(new CommandDefinition(sql, parameters, cancellationToken: ct));
        }, sql, parameters) ?? Array.Empty<Album>();
    }

    /// <summary>Gets all tracks by artist ID.</summary>
    public async Task<IEnumerable<Track>> GetTracksAsync(long artistId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT t.*, ar.Name AS ArtistName, al.Title AS AlbumTitle, g.Name AS GenreName
            FROM Tracks t
            LEFT JOIN Artists ar ON t.ArtistId = ar.Id
            LEFT JOIN Albums al ON t.AlbumId = al.Id
            LEFT JOIN Genres g ON t.GenreId = g.Id
            WHERE t.ArtistId = @artistId
            ORDER BY al.Year DESC, al.Title ASC, t.TrackNo ASC";

        var parameters = new { artistId };

        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.QueryAsync<Track>(new CommandDefinition(sql, parameters, cancellationToken: ct));
        }, sql, parameters) ?? Array.Empty<Track>();
    }

    /// <summary>Inserts or updates an artist.</summary>
    public async Task<long> UpsertAsync(Artist artist)
    {
        const string sql = @"
            INSERT INTO Artists (Name, NormalizedName, CoverPath) 
            VALUES (@Name, @NormalizedName, @CoverPath) 
            ON CONFLICT(NormalizedName) DO UPDATE SET 
              Name = excluded.Name, 
              CoverPath = COALESCE(excluded.CoverPath, Artists.CoverPath) 
            RETURNING Id";

        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.ExecuteScalarAsync<long>(sql, artist);
        }, sql, artist);
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
