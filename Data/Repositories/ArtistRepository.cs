using Dapper;
using MediaMusic.Data.Models;

namespace MediaMusic.Data.Repositories;

/// <summary>Dapper-backed repository for <see cref="Artist"/> entities.</summary>
public sealed class ArtistRepository
{
    private readonly DbConnectionFactory _factory;

    public ArtistRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<Artist?> GetByIdAsync(long id)
    {
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<Artist>(
            "SELECT * FROM Artists WHERE Id = @id", new { id });
    }

    public async Task<long> UpsertAsync(Artist artist)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<long>(
            "INSERT INTO Artists (Name, NormalizedName, CoverPath) " +
            "VALUES (@Name, @NormalizedName, @CoverPath) " +
            "ON CONFLICT(NormalizedName) DO UPDATE SET " +
            "  Name = excluded.Name, " +
            "  CoverPath = COALESCE(excluded.CoverPath, Artists.CoverPath) " +
            "RETURNING Id", artist);
    }
}
