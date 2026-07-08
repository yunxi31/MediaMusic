using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Dapper;
using MediaMusic.Data.Models;

namespace MediaMusic.Data.Repositories;

/// <summary>
/// Dapper-backed repository for <see cref="EqPreset"/> entities.
/// </summary>
public sealed class EqPresetRepository
{
    private readonly DbConnectionFactory _factory;
    private readonly ILogger<EqPresetRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EqPresetRepository"/> class.
    /// </summary>
    public EqPresetRepository(DbConnectionFactory factory, ILogger<EqPresetRepository> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <summary>Gets all presets ordered by built-in first, then name.</summary>
    public async Task<IEnumerable<EqPreset>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM EqPresets ORDER BY IsBuiltIn DESC, Name ASC";
        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.QueryAsync<EqPreset>(new CommandDefinition(sql, cancellationToken: ct));
        }, sql, null) ?? Array.Empty<EqPreset>();
    }

    /// <summary>Gets a preset by its ID.</summary>
    public async Task<EqPreset?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM EqPresets WHERE Id = @id";
        var parameters = new { id };

        return await ExecuteWithRetryAsync(async (conn) =>
        {
            return await conn.QueryFirstOrDefaultAsync<EqPreset>(new CommandDefinition(sql, parameters, cancellationToken: ct));
        }, sql, parameters);
    }

    /// <summary>Creates a new user preset.</summary>
    /// <exception cref="InvalidOperationException">Thrown if a preset with the same name already exists.</exception>
    /// <exception cref="ArgumentException">Thrown if name is empty.</exception>
    public async Task<long> CreateAsync(string name, IEnumerable<EqBand> bands, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Preset name cannot be empty.", nameof(name));

        const string checkSql = "SELECT COUNT(*) FROM EqPresets WHERE Name = @name";
        const string insertSql = @"
            INSERT INTO EqPresets (Name, Bands, IsBuiltIn, CreatedAt) 
            VALUES (@name, @json, 0, datetime('now'))
            RETURNING Id";

        var json = JsonSerializer.Serialize(bands);
        var checkParams = new { name };
        var insertParams = new { name, json };

        return await ExecuteWithRetryAsync(async (conn) =>
        {
            var count = await conn.ExecuteScalarAsync<int>(new CommandDefinition(checkSql, checkParams, cancellationToken: ct));
            if (count > 0)
                throw new InvalidOperationException($"A preset named '{name}' already exists.");

            return await conn.ExecuteScalarAsync<long>(new CommandDefinition(insertSql, insertParams, cancellationToken: ct));
        }, insertSql, insertParams);
    }

    /// <summary>Deletes a user-created preset.</summary>
    /// <exception cref="InvalidOperationException">Thrown if attempting to delete a built-in preset.</exception>
    public async Task<int> DeleteAsync(long id, CancellationToken ct = default)
    {
        const string checkSql = "SELECT IsBuiltIn FROM EqPresets WHERE Id = @id";
        const string deleteSql = "DELETE FROM EqPresets WHERE Id = @id";
        var parameters = new { id };

        return await ExecuteWithRetryAsync(async (conn) =>
        {
            var isBuiltInResult = await conn.QueryFirstOrDefaultAsync<bool?>(new CommandDefinition(checkSql, parameters, cancellationToken: ct));
            if (isBuiltInResult == null)
                return 0; // Preset not found, complete without error

            if (isBuiltInResult.Value)
                throw new InvalidOperationException($"Cannot delete built-in preset (ID: {id}).");

            return await conn.ExecuteAsync(new CommandDefinition(deleteSql, parameters, cancellationToken: ct));
        }, deleteSql, parameters);
    }

    /// <summary>Saves or updates a preset by name.</summary>
    public async Task<long> SaveAsync(string name, IEnumerable<EqBand> bands)
    {
        var json = JsonSerializer.Serialize(bands);
        return await ExecuteWithRetryAsync(async (conn) =>
        {
            var existingId = await conn.QueryFirstOrDefaultAsync<long?>(
                "SELECT Id FROM EqPresets WHERE Name = @name", new { name });
            if (existingId.HasValue)
            {
                await conn.ExecuteAsync(
                    "UPDATE EqPresets SET Bands = @json WHERE Id = @id", new { json, id = existingId.Value });
                return existingId.Value;
            }
            else
            {
                return await conn.QuerySingleAsync<long>(
                    "INSERT INTO EqPresets (Name, Bands, IsBuiltIn) VALUES (@name, @json, 0); SELECT last_insert_rowid();",
                    new { name, json });
            }
        }, "SaveAsync", new { name, json });
    }

    /// <summary>Loads bands for a preset by ID.</summary>
    public async Task<IEnumerable<EqBand>> LoadBandsAsync(long presetId)
    {
        return await ExecuteWithRetryAsync(async (conn) =>
        {
            var json = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT Bands FROM EqPresets WHERE Id = @presetId", new { presetId });
            if (string.IsNullOrEmpty(json))
                return Array.Empty<EqBand>();
            return JsonSerializer.Deserialize<IEnumerable<EqBand>>(json) ?? Array.Empty<EqBand>();
        }, "LoadBandsAsync", new { presetId }) ?? Array.Empty<EqBand>();
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
