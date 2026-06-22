using MediaMusic.Data.Models;

namespace MediaMusic.Data.Repositories;

/// <summary>Dapper-backed repository for <see cref="Playlist"/> and <see cref="PlaylistTrack"/>.</summary>
public sealed class PlaylistRepository
{
    private readonly DbConnectionFactory _factory;

    public PlaylistRepository(DbConnectionFactory factory) => _factory = factory;

    public Task<long> CreateAsync(string name)
    {
        // TODO: INSERT INTO Playlists (Name) VALUES (@name); return Id.
        throw new NotImplementedException();
    }

    public Task AddTrackAsync(long playlistId, long trackId, int sortOrder)
    {
        // TODO: INSERT INTO PlaylistTracks (PlaylistId, TrackId, SortOrder) VALUES (...).
        throw new NotImplementedException();
    }

    public Task<IEnumerable<Track>> GetTracksAsync(long playlistId)
    {
        // TODO: JOIN PlaylistTracks -> Tracks ordered by SortOrder.
        throw new NotImplementedException();
    }
}
