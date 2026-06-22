using Microsoft.Data.Sqlite;

namespace MediaMusic.Data;

/// <summary>
/// Produces open-ready <see cref="SqliteConnection"/> instances pointing at the
/// user library database located at <c>%APPDATA%/MediaMusic/library.db</c>.
/// The database file (and its parent folder) are created lazily by
/// <see cref="DbInitializer"/> on first use.
/// </summary>
public sealed class DbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MediaMusic");
        Directory.CreateDirectory(folder);
        var dbPath = Path.Combine(folder, "library.db");
        _connectionString = $"Data Source={dbPath}";
    }

    /// <summary>The absolute path of the library database file.</summary>
    public string DatabasePath => new SqliteConnectionStringBuilder(_connectionString).DataSource!;

    /// <summary>Creates a new, unopened connection. Callers are responsible for disposing it.</summary>
    public SqliteConnection Create() => new(_connectionString);
}
