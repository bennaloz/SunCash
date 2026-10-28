using Microsoft.EntityFrameworkCore;

namespace SunCash.Infrastructure.Persistence;

/// <summary>
/// All'avvio: applica le migrazioni e attiva il WAL, così l'API legge mentre un job scrive.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>Porta il database all'ultima versione dello schema.</summary>
    public static async Task InitializeAsync(SunCashDbContext db, CancellationToken ct)
    {
        await db.Database.MigrateAsync(ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
    }
}
