using Dapper;
using MediaMusic.Data.Models;

namespace MediaMusic.Data.Repositories;

/// <summary>
/// Dapper-backed repository for <see cref="Track"/> entities. Supports the
/// album/artist/genre cross-search required by PRD §2.2.
/// </summary>
public sealed class TrackRepository
{
    private readonly DbConnectionFactory _factory;

    public TrackRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<Track?> GetByIdAsync(long id)
    {
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<Track>(
            @"SELECT t.*, ar.Name AS ArtistName, al.Title AS AlbumTitle, g.Name AS GenreName
              FROM Tracks t
              LEFT JOIN Artists ar ON t.ArtistId = ar.Id
              LEFT JOIN Albums al ON t.AlbumId = al.Id
              LEFT JOIN Genres g ON t.GenreId = g.Id
              WHERE t.Id = @id", new { id });
    }

    public async Task<IEnumerable<Track>> SearchAsync(long? artistId, long? albumId, long? genreId, string? title)
    {
        using var conn = _factory.Create();
        var sql = @"
            SELECT t.*, ar.Name AS ArtistName, al.Title AS AlbumTitle, g.Name AS GenreName
            FROM Tracks t
            LEFT JOIN Artists ar ON t.ArtistId = ar.Id
            LEFT JOIN Albums al ON t.AlbumId = al.Id
            LEFT JOIN Genres g ON t.GenreId = g.Id
            WHERE 1=1";
        
        var parameters = new DynamicParameters();
        if (artistId.HasValue)
        {
            sql += " AND t.ArtistId = @artistId";
            parameters.Add("artistId", artistId.Value);
        }
        if (albumId.HasValue)
        {
            sql += " AND t.AlbumId = @albumId";
            parameters.Add("albumId", albumId.Value);
        }
        if (genreId.HasValue)
        {
            sql += " AND t.GenreId = @genreId";
            parameters.Add("genreId", genreId.Value);
        }
        if (!string.IsNullOrEmpty(title))
        {
            sql += " AND (t.Title LIKE @title OR ar.Name LIKE @title OR al.Title LIKE @title)";
            parameters.Add("title", $"%{title}%");
        }
        
        sql += " ORDER BY t.DateAdded DESC, t.Title ASC";
        return await conn.QueryAsync<Track>(sql, parameters);
    }

    public async Task<long> UpsertAsync(Track track)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<long>(
            @"INSERT INTO Tracks (
                FilePath, Title, ArtistId, AlbumId, GenreId, TrackNo, Year,
                DurationMs, BitRate, SampleRate, Channels, Format, CoverPath,
                DateAdded, LastPlayed, PlayCount, IsFavourite
            ) VALUES (
                @FilePath, @Title, @ArtistId, @AlbumId, @GenreId, @TrackNo, @Year,
                @DurationMs, @BitRate, @SampleRate, @Channels, @Format, @CoverPath,
                COALESCE(@DateAdded, datetime('now')), @LastPlayed, @PlayCount, @IsFavourite
            )
            ON CONFLICT(FilePath) DO UPDATE SET
                Title = COALESCE(excluded.Title, Tracks.Title),
                ArtistId = COALESCE(excluded.ArtistId, Tracks.ArtistId),
                AlbumId = COALESCE(excluded.AlbumId, Tracks.AlbumId),
                GenreId = COALESCE(excluded.GenreId, Tracks.GenreId),
                TrackNo = COALESCE(excluded.TrackNo, Tracks.TrackNo),
                Year = COALESCE(excluded.Year, Tracks.Year),
                DurationMs = COALESCE(excluded.DurationMs, Tracks.DurationMs),
                BitRate = COALESCE(excluded.BitRate, Tracks.BitRate),
                SampleRate = COALESCE(excluded.SampleRate, Tracks.SampleRate),
                Channels = COALESCE(excluded.Channels, Tracks.Channels),
                Format = COALESCE(excluded.Format, Tracks.Format),
                CoverPath = COALESCE(excluded.CoverPath, Tracks.CoverPath),
                IsFavourite = COALESCE(excluded.IsFavourite, Tracks.IsFavourite)
            RETURNING Id", track);
    }

    public async Task IncrementPlayCountAsync(long id)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            "UPDATE Tracks SET PlayCount = PlayCount + 1, LastPlayed = datetime('now') WHERE Id = @id", new { id });
    }

    /// <summary>Toggles a track's favourite status. Returns the new value.</summary>
    public async Task<bool> ToggleFavouriteAsync(long id)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            "UPDATE Tracks SET IsFavourite = CASE WHEN IsFavourite = 0 THEN 1 ELSE 0 END WHERE Id = @id", new { id });
        return await conn.QueryFirstOrDefaultAsync<bool>(
            "SELECT IsFavourite FROM Tracks WHERE Id = @id", new { id });
    }

    /// <summary>Returns all favourited tracks.</summary>
    public async Task<IEnumerable<Track>> GetFavouritesAsync()
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Track>(
            @"SELECT t.*, ar.Name AS ArtistName, al.Title AS AlbumTitle, g.Name AS GenreName
              FROM Tracks t
              LEFT JOIN Artists ar ON t.ArtistId = ar.Id
              LEFT JOIN Albums al ON t.AlbumId = al.Id
              LEFT JOIN Genres g ON t.GenreId = g.Id
              WHERE t.IsFavourite = 1
              ORDER BY t.LastPlayed DESC, t.Title ASC");
    }

    public async Task DeleteAsync(long id)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("DELETE FROM Tracks WHERE Id = @id", new { id });
    }
}
