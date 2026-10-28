using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using SunCash.Infrastructure.DbBackup;
using SunCash.Infrastructure.Sync;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace SunCash.Infrastructure.Tests;

public sealed class DatabaseBackupJobTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"suncash-backup-{Guid.NewGuid():N}");

    [Fact]
    public async Task Crea_una_copia_leggibile_e_cancella_quelle_troppo_vecchie()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "suncash-20261001.db"), "vecchia");
        File.WriteAllText(Path.Combine(_dir, "suncash-20261020.db"), "recente");
        File.WriteAllText(Path.Combine(_dir, "altro-file.db"), "non mio");
        await using var db = await TestDatabase.CreateAsync();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 28, 2, 17, 0, TimeSpan.Zero));

        await using var context = db.NewContext();
        var outcome = await new DatabaseBackupJob(context, MsOptions.Create(new BackupOptions { Directory = _dir, KeepDays = 14 }), clock)
            .RunAsync(CancellationToken.None);

        Assert.Equal(SyncStatus.Done, outcome.Status);
        var copy = Path.Combine(_dir, "suncash-20261028.db");
        await using (var connection = new SqliteConnection($"Data Source={copy};Mode=ReadOnly;Pooling=False"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE name = 'market_prices'";
            Assert.Equal(1L, await command.ExecuteScalarAsync());
        }

        Assert.False(File.Exists(Path.Combine(_dir, "suncash-20261001.db")));
        Assert.True(File.Exists(Path.Combine(_dir, "suncash-20261020.db")));
        Assert.True(File.Exists(Path.Combine(_dir, "altro-file.db")));
    }

    [Fact]
    public async Task Senza_cartella_il_backup_e_spento_e_lo_dice()
    {
        await using var db = await TestDatabase.CreateAsync();
        await using var context = db.NewContext();

        var outcome = await new DatabaseBackupJob(context, MsOptions.Create(new BackupOptions()), TimeProvider.System)
            .RunAsync(CancellationToken.None);

        Assert.Equal(SyncStatus.Failed, outcome.Status);
        Assert.Contains("Backup:Directory", outcome.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}

public sealed class DatabaseRestoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"suncash-restore-{Guid.NewGuid():N}");

    [Fact]
    public void Se_il_database_manca_riparte_dalla_copia_piu_recente()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "backups"));
        File.WriteAllText(BackupFiles.PathFor(Path.Combine(_dir, "backups"), new DateOnly(2026, 10, 26)), "vecchia");
        File.WriteAllText(BackupFiles.PathFor(Path.Combine(_dir, "backups"), new DateOnly(2026, 10, 27)), "recente");
        var database = Path.Combine(_dir, "data", "suncash.db");

        var used = DatabaseRestore.RestoreIfMissing($"Data Source={database}", Path.Combine(_dir, "backups"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        Assert.EndsWith("suncash-20261027.db", used);
        Assert.Equal("recente", File.ReadAllText(database));
    }

    [Fact]
    public void Se_il_database_c_e_non_lo_tocca()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "backups"));
        File.WriteAllText(BackupFiles.PathFor(Path.Combine(_dir, "backups"), new DateOnly(2026, 10, 27)), "copia");
        var database = Path.Combine(_dir, "suncash.db");
        File.WriteAllText(database, "vivo");

        var used = DatabaseRestore.RestoreIfMissing($"Data Source={database}", Path.Combine(_dir, "backups"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        Assert.Null(used);
        Assert.Equal("vivo", File.ReadAllText(database));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
