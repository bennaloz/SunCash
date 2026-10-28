using Microsoft.EntityFrameworkCore;
using SunCash.Infrastructure.Persistence;

namespace SunCash.Infrastructure.Tests;

/// <summary>
/// Un database SQLite vero su un file temporaneo, uno per test: niente stato condiviso fra test
/// che girano in parallelo, niente provider InMemory che non conosce SQL.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"suncash-test-{Guid.NewGuid():N}.db");

    /// <summary>Stringa di connessione al file.</summary>
    public string ConnectionString => $"Data Source={_path};Pooling=False";

    /// <summary>Un contesto nuovo sullo stesso file.</summary>
    public SunCashDbContext NewContext() =>
        new(new DbContextOptionsBuilder<SunCashDbContext>().UseSqlite(ConnectionString).Options);

    /// <summary>Crea il file e applica le migrazioni.</summary>
    public static async Task<TestDatabase> CreateAsync()
    {
        var db = new TestDatabase();
        await using var context = db.NewContext();
        await DatabaseInitializer.InitializeAsync(context, CancellationToken.None);
        return db;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(file))
                File.Delete(file);
        return ValueTask.CompletedTask;
    }
}
