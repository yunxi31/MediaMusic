using Dapper;
using MediaMusic.Data.Models;

namespace MediaMusic.Data.Repositories;

/// <summary>Dapper-backed repository for <see cref="Playlist"/> and <see cref="PlaylistTrack"/>.</summary>
public sealed class PlaylistRepository
{
    private readonly DbConnectionFactory _factory;

    public PlaylistRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<long> CreateAsync(string name)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<long>(
            "INSERT INTO Playlists (Name, CreatedAt) VALUES (@name, datetime('now')) RETURNING Id", new { name });
    }

    public async Task AddTrackAsync(long playlistId, long trackId, int sortOrder)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            "INSERT OR REPLACE INTO PlaylistTracks (PlaylistId, TrackId, SortOrder) VALUES (@playlistId, @trackId, @sortOrder)",
            new { playlistId, trackId, sortOrder });
    }

    public async Task<IEnumerable<Track>> GetTracksAsync(long playlistId)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Track>(
            @"SELECT t.*, ar.Name AS ArtistName, al.Title AS AlbumTitle, g.Name AS GenreName
              FROM PlaylistTracks pt
              JOIN Tracks t ON pt.TrackId = t.Id
              LEFT JOIN Artists ar ON t.ArtistId = ar.Id
              LEFT JOIN Albums al ON t.AlbumId = al.Id
              LEFT JOIN Genres g ON t.GenreId = g.Id
              WHERE pt.PlaylistId = @playlistId
              ORDER BY pt.SortOrder ASC, t.Title ASC", new { playlistId });
    }

    public async Task<IEnumerable<Playlist>> GetAllAsync()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Playlist>("SELECT * FROM Playlists ORDER BY CreatedAt DESC");
    }

    public async Task<Playlist?> GetByIdAsync(long id)
    {
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<Playlist>("SELECT * FROM Playlists WHERE Id = @id", new { id });
    }

    public async Task DeleteAsync(long id)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("DELETE FROM Playlists WHERE Id = @id", new { id });
    }

    public async Task RemoveTrackAsync(long playlistId, long trackId)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("DELETE FROM PlaylistTracks WHERE PlaylistId = @playlistId AND TrackId = @trackId", new { playlistId, trackId });
    }
}
