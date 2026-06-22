using System.Text.Json;
using Dapper;
using MediaMusic.Data.Models;

namespace MediaMusic.Data.Repositories;

/// <summary>Dapper-backed repository for <see cref="EqPreset"/> entities (PRD §2.1 EQ presets).</summary>
public sealed class EqPresetRepository
{
    private readonly DbConnectionFactory _factory;

    public EqPresetRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<EqPreset>> GetAllAsync()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<EqPreset>(
            "SELECT * FROM EqPresets ORDER BY IsBuiltIn DESC, Name");
    }

    public async Task<long> SaveAsync(string name, IEnumerable<EqBand> bands)
    {
        using var conn = _factory.Create();
        var json = JsonSerializer.Serialize(bands);
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
    }

    public async Task<IEnumerable<EqBand>> LoadBandsAsync(long presetId)
    {
        using var conn = _factory.Create();
        var json = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT Bands FROM EqPresets WHERE Id = @presetId", new { presetId });
        if (string.IsNullOrEmpty(json))
            return Array.Empty<EqBand>();
        return JsonSerializer.Deserialize<IEnumerable<EqBand>>(json) ?? Array.Empty<EqBand>();
    }
}
