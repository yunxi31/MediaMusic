using System.Reflection;
using Dapper;

namespace MediaMusic.Data;

/// <summary>
/// Ensures the library database schema exists. On startup it executes the
/// embedded <c>schema.sql</c> (idempotent due to <c>IF NOT EXISTS</c>) and then
/// seeds built-in EQ presets if the <see cref="Models.EqPreset"/> table is empty.
/// </summary>
public sealed class DbInitializer
{
    private readonly DbConnectionFactory _factory;
    private readonly SeedData _seed;
    private readonly ILogger<DbInitializer> _logger;
    private int _initialized; // 0 = not run, 1 = done (interlocked)

    public DbInitializer(DbConnectionFactory factory, SeedData seed, ILogger<DbInitializer> logger)
    {
        _factory = factory;
        _seed = seed;
        _logger = logger;
    }

    /// <summary>Runs schema migration + seeding exactly once per process.</summary>
    public void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
            return;

        try
        {
            var schema = ReadEmbeddedSchema();
            using var conn = _factory.Create();
            conn.Open();
            conn.Execute(schema);
            _seed.SeedIfEmpty(conn);
            _logger.LogInformation("Library database ready at {Path}", _factory.DatabasePath);
        }
        catch (Exception ex)
        {
            // Skeleton: log and continue so the UI still starts without a DB.
            _logger.LogError(ex, "Failed to initialize library database.");
        }
    }

    private static string ReadEmbeddedSchema()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "MediaMusic.Data.schema.sql";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
