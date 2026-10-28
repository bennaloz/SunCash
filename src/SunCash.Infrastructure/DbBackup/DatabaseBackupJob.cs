using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SunCash.Domain.Time;
using SunCash.Infrastructure.Persistence;
using SunCash.Infrastructure.Sync;

namespace SunCash.Infrastructure.DbBackup;

/// <summary>
/// Job <c>db-backup</c>: una copia coerente del database al giorno con <c>VACUUM INTO</c> (mai una
/// copia del file a database aperto), e le copie più vecchie di <see cref="BackupOptions.KeepDays"/>
/// cancellate. Gira nel processo dell'app, che è l'unico a scrivere sul file.
/// </summary>
public sealed class DatabaseBackupJob(SunCashDbContext db, IOptions<BackupOptions> options, TimeProvider clock) : ISyncJob
{
    /// <summary>Nome del job.</summary>
    public const string JobName = "db-backup";

    private const string Prefix = "suncash-";

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
        var target = Path.Combine(directory, $"{Prefix}{today:yyyyMMdd}.db");
        if (File.Exists(target))
            File.Delete(target); // VACUUM INTO non sovrascrive

        await db.Database.ExecuteSqlAsync($"VACUUM INTO {target}", ct);

        var oldest = today.AddDays(-options.Value.KeepDays + 1);
        foreach (var file in Directory.EnumerateFiles(directory, $"{Prefix}*.db"))
            if (DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(file)[Prefix.Length..], "yyyyMMdd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) && day < oldest)
                File.Delete(file);

        return SyncOutcome.Done();
    }
}
