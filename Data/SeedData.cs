using System.Text.Json;
using Dapper;
using MediaMusic.Data.Models;
using Microsoft.Data.Sqlite;

namespace MediaMusic.Data;

/// <summary>
/// Seeds the built-in equalizer presets (Rock / Bass Boost / Classical / Flat) into
/// the <see cref="EqPreset"/> table the first time the database is created.
/// </summary>
public sealed class SeedData
{
    private static readonly EqBand[] DefaultBands =
    {
        new() { Frequency = 60 }, new() { Frequency = 170 }, new() { Frequency = 310 },
        new() { Frequency = 600 }, new() { Frequency = 1000 }, new() { Frequency = 3000 },
        new() { Frequency = 6000 }, new() { Frequency = 12000 }, new() { Frequency = 14000 },
        new() { Frequency = 16000 }
    };

    private static readonly (string Name, double[] Gains)[] BuiltInPresets =
    {
        ("Flat",        new double[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Rock",        new double[] { 4, 3, 2, 0, -1, 1, 3, 4, 4, 4 }),
        ("Bass Boost",  new double[] { 6, 5, 4, 2, 0, 0, 0, 0, 0, 0 }),
        ("Classical",   new double[] { 3, 2, 1, 0, -1, -1, 0, 2, 3, 3 })
    };

    /// <summary>Inserts built-in presets only if the table is currently empty.</summary>
    public void SeedIfEmpty(SqliteConnection conn)
    {
        var count = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM EqPresets");
        if (count > 0)
            return;

        foreach (var (name, gains) in BuiltInPresets)
        {
            var bands = DefaultBands
                .Select((b, i) => new EqBand { Frequency = b.Frequency, Gain = gains[i] })
                .ToArray();
            var json = JsonSerializer.Serialize(bands);
            conn.Execute(
                "INSERT INTO EqPresets (Name, Bands, IsBuiltIn) VALUES (@Name, @Bands, 1)",
                new { Name = name, Bands = json });
        }
    }
}
