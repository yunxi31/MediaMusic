using System.Text.Json;
using MediaMusic.Data.Models;

namespace MediaMusic.Data.Repositories;

/// <summary>Dapper-backed repository for <see cref="EqPreset"/> entities (PRD §2.1 EQ presets).</summary>
public sealed class EqPresetRepository
{
    private readonly DbConnectionFactory _factory;

    public EqPresetRepository(DbConnectionFactory factory) => _factory = factory;

    public Task<IEnumerable<EqPreset>> GetAllAsync()
    {
        // TODO: SELECT * FROM EqPresets ORDER BY IsBuiltIn DESC, Name.
        throw new NotImplementedException();
    }

    public Task<long> SaveAsync(string name, IEnumerable<EqBand> bands)
    {
        // TODO: serialize bands to JSON, INSERT/UPDATE EqPresets.
        throw new NotImplementedException();
    }

    public Task<IEnumerable<EqBand>> LoadBandsAsync(long presetId)
    {
        // TODO: SELECT Bands, deserialize JSON -> EqBand[].
        throw new NotImplementedException();
    }
}
