using MediaMusic.Data;
using MediaMusic.Data.Repositories;

namespace MediaMusic.Services;

/// <summary>
/// Thin wrapper over <see cref="SettingsRepository"/> providing typed access to
/// the key/value <c>Settings</c> table (theme, crossfade duration, scan roots, ...).
/// </summary>
public sealed class SettingsService
{
    private readonly SettingsRepository _repo;
    private readonly DbInitializer _dbInit;

    public SettingsService(SettingsRepository repo, DbInitializer dbInit)
    {
        _repo = repo;
        _dbInit = dbInit;
    }

    public async Task<string?> GetAsync(string key)
    {
        _dbInit.Initialize();
        return await _repo.GetAsync(key);
    }

    public async Task SetAsync(string key, string? value)
    {
        _dbInit.Initialize();
        await _repo.SetAsync(key, value);
    }

    public async Task<T?> GetAsync<T>(string key, T fallback)
    {
        var raw = await GetAsync(key);
        if (string.IsNullOrEmpty(raw))
            return fallback;
        try { return (T)Convert.ChangeType(raw, typeof(T)); }
        catch { return fallback; }
    }
}
