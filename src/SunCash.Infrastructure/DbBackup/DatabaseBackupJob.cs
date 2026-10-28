using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SunCash.Domain.Time;
using SunCash.Infrastructure.Persistence;
using SunCash.Infrastructure.Sync;

namespace SunCash.Infrastructure.DbBackup;

/// <summary>
/// Job <c>db-backup</c>: una copia coerente del database al giorno con <c>VACUUM INTO</c> (mai una
/// copia del file a database aperto), e le copie più vecchie di <see cref="BackupOptions.KeepDays"/>
/// cancellate. Gira nel processo dell'app, che è l'unico a scrivere sul file. La copia si scrive
/// prima nella cartella temporanea locale e poi si sposta nella cartella dei backup: così SQLite non
/// apre mai un file su un disco di rete (su Azure la cartella dei backup è Azure Files).
/// </summary>
public sealed class DatabaseBackupJob(SunCashDbContext db, IOptions<BackupOptions> options, TimeProvider clock) : ISyncJob
{
    /// <summary>Nome del job.</summary>
    public const string JobName = "db-backup";

    /// <inheritdoc />
    public string Name => JobName;

    /// <inheritdoc />
    public async Task<SyncOutcome> RunAsync(CancellationToken ct)
    {
        var directory = options.Value.Directory;
        if (string.IsNullOrWhiteSpace(directory))
            return SyncOutcome.Failed("Backup spento: manca Backup:Directory.");

        Directory.CreateDirectory(directory);
        var today = ItalianTime.LocalDate(clock.GetUtcNow());
        var local = Path.Combine(Path.GetTempPath(), $"suncash-backup-{Guid.NewGuid():N}.db");
        try
        {
            await db.Database.ExecuteSqlAsync($"VACUUM INTO {local}", ct);
            File.Copy(local, BackupFiles.PathFor(directory, today), overwrite: true);
        }
        finally
        {
            File.Delete(local);
        }

        var oldest = today.AddDays(-options.Value.KeepDays + 1);
        foreach (var (day, path) in BackupFiles.List(directory))
            if (day < oldest)
                File.Delete(path);

        return SyncOutcome.Done();
    }
}
