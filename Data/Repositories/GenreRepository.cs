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
/// Dapper-backed repository for <see cref="Genre"/> entities.
/// </summary>
public sealed class GenreRepository
{
    private readonly DbConnectionFactory _factory;
    private readonly ILogger<GenreRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenreRepository"/> class.
    /// </summary>
    public GenreRepository(DbConnectionFactory factory, ILogger<GenreRepository> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <summary>Gets all genres ordered by name.</summary>
    public async Task<IEnumerable<Genre>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM Genres ORDER BY Name ASC";
        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.QueryAsync<Genre>(new CommandDefinition(sql, cancellationToken: ct));
        }, sql, null) ?? Array.Empty<Genre>();
    }

    /// <summary>Gets a genre by ID.</summary>
    public async Task<Genre?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM Genres WHERE Id = @id";
        var parameters = new { id };

        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.QueryFirstOrDefaultAsync<Genre>(new CommandDefinition(sql, parameters, cancellationToken: ct));
        }, sql, parameters);
    }

    /// <summary>Gets all tracks in a genre ordered by artist name, album title, and track number.</summary>
    public async Task<IEnumerable<Track>> GetTracksAsync(long genreId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT t.*, ar.Name AS ArtistName, al.Title AS AlbumTitle, g.Name AS GenreName
            FROM Tracks t
            LEFT JOIN Artists ar ON t.ArtistId = ar.Id
            LEFT JOIN Albums al ON t.AlbumId = al.Id
            LEFT JOIN Genres g ON t.GenreId = g.Id
            WHERE t.GenreId = @genreId
            ORDER BY ar.Name ASC, al.Title ASC, t.TrackNo ASC";

        var parameters = new { genreId };

        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.QueryAsync<Track>(new CommandDefinition(sql, parameters, cancellationToken: ct));
        }, sql, parameters) ?? Array.Empty<Track>();
    }

    /// <summary>Inserts or updates a genre.</summary>
    public async Task<long> UpsertAsync(string name)
    {
        const string sql = @"
            INSERT INTO Genres (Name) VALUES (@name) 
            ON CONFLICT(Name) DO UPDATE SET Name = Name 
            RETURNING Id";
        var parameters = new { name };

        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.ExecuteScalarAsync<long>(sql, parameters);
        }, sql, parameters);
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
