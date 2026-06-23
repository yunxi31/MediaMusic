-- MediaMusic library schema
-- Executed by DbInitializer on first run against %APPDATA%/MediaMusic/library.db.
-- Tracks are linked to Artists / Albums / Genres by foreign key so album/artist/genre
-- cross-search can hit dedicated indexes instead of scanning the whole Tracks table.

PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Artists (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    Name            TEXT NOT NULL,
    NormalizedName  TEXT NOT NULL,
    CoverPath       TEXT,
    UNIQUE(NormalizedName)
);

CREATE TABLE IF NOT EXISTS Albums (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    Title           TEXT NOT NULL,
    NormalizedTitle TEXT NOT NULL,
    ArtistId        INTEGER,
    Year            INTEGER,
    CoverPath       TEXT,
    FOREIGN KEY (ArtistId) REFERENCES Artists(Id),
    UNIQUE(NormalizedTitle, ArtistId)
);

CREATE TABLE IF NOT EXISTS Genres (
    Id   INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Tracks (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    FilePath    TEXT NOT NULL UNIQUE,
    Title       TEXT,
    ArtistId    INTEGER,
    AlbumId     INTEGER,
    GenreId     INTEGER,
    TrackNo     INTEGER,
    Year        INTEGER,
    DurationMs  INTEGER,
    BitRate     INTEGER,
    SampleRate  INTEGER,
    Channels    INTEGER,
    Format      TEXT,                -- FLAC / APE / WAV / MP3 / AAC / OGG
    CoverPath   TEXT,
    DateAdded   TEXT NOT NULL DEFAULT (datetime('now')),
    LastPlayed  TEXT,
    PlayCount   INTEGER NOT NULL DEFAULT 0,
    IsFavourite INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (ArtistId) REFERENCES Artists(Id),
    FOREIGN KEY (AlbumId)  REFERENCES Albums(Id),
    FOREIGN KEY (GenreId)  REFERENCES Genres(Id)
);

CREATE INDEX IF NOT EXISTS IX_Tracks_AlbumId  ON Tracks(AlbumId);
CREATE INDEX IF NOT EXISTS IX_Tracks_ArtistId ON Tracks(ArtistId);
CREATE INDEX IF NOT EXISTS IX_Tracks_GenreId  ON Tracks(GenreId);
CREATE INDEX IF NOT EXISTS IX_Tracks_Title    ON Tracks(Title);
CREATE INDEX IF NOT EXISTS IX_Tracks_Format   ON Tracks(Format);

CREATE TABLE IF NOT EXISTS Playlists (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    Name      TEXT NOT NULL,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS PlaylistTracks (
    PlaylistId INTEGER NOT NULL,
    TrackId    INTEGER NOT NULL,
    SortOrder  INTEGER NOT NULL,
    PRIMARY KEY (PlaylistId, TrackId),
    FOREIGN KEY (PlaylistId) REFERENCES Playlists(Id) ON DELETE CASCADE,
    FOREIGN KEY (TrackId)    REFERENCES Tracks(Id)    ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_PlaylistTracks_TrackId ON PlaylistTracks(TrackId);

-- EQ preset bands stored as JSON: [{ "freq": 60, "gain": 3.0 }, ... ] (10+ bands)
CREATE TABLE IF NOT EXISTS EqPresets (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    Name      TEXT NOT NULL UNIQUE,
    Bands     TEXT NOT NULL,
    IsBuiltIn INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS Settings (
    [Key]   TEXT PRIMARY KEY,
    [Value] TEXT
);
