using Dapper;
using MediaMusic.Data.Models;

namespace MediaMusic.Data.Repositories;

/// <summary>Dapper-backed repository for <see cref="Genre"/> entities.</summary>
public sealed class GenreRepository
{
    private readonly DbConnectionFactory _factory;

    public GenreRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<long> UpsertAsync(string name)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<long>(
            "INSERT INTO Genres (Name) VALUES (@name) " +
            "ON CONFLICT(Name) DO UPDATE SET Name = Name " +
            "RETURNING Id", new { name });
    }
}
