using Dapper;

namespace MediaMusic.Data.Repositories;

/// <summary>Dapper-backed key/value repository for the <c>Settings</c> table.</summary>
public sealed class SettingsRepository
{
    private readonly DbConnectionFactory _factory;

    public SettingsRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<string?> GetAsync(string key)
    {
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<string?>(
            "SELECT Value FROM Settings WHERE [Key] = @key", new { key });
    }

    public async Task SetAsync(string key, string? value)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            "INSERT INTO Settings ([Key], Value) VALUES (@key, @value) " +
            "ON CONFLICT([Key]) DO UPDATE SET Value = excluded.Value", new { key, value });
    }
}
