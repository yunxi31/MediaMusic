using Dapper;
using MediaMusic.Data.Models;

namespace MediaMusic.Data.Repositories;

/// <summary>Dapper-backed repository for <see cref="Album"/> entities.</summary>
public sealed class AlbumRepository
{
    private readonly DbConnectionFactory _factory;

    public AlbumRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<Album?> GetByIdAsync(long id)
    {
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<Album>(
            "SELECT * FROM Albums WHERE Id = @id", new { id });
    }

    public async Task<long> UpsertAsync(Album album)
    {
        using var conn = _factory.Create();
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
    }
}
