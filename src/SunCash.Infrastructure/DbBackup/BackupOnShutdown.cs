using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SunCash.Infrastructure.Sync;

namespace SunCash.Infrastructure.DbBackup;

/// <summary>
/// Un backup anche quando l'app si ferma (nuovo deploy, riavvio): dove il disco del database non
/// sopravvive, riduce le modifiche perse dopo l'ultimo backup orario. Non è garantito: se la
/// piattaforma ferma il container senza preavviso non gira (successo il 2026-11-07 e il 2026-11-08).
/// </summary>
public sealed class BackupOnShutdown(SyncJobRunner runner, IOptions<BackupOptions> options) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.Value.Directory))
            await runner.RunAsync(DatabaseBackupJob.JobName, cancellationToken);
    }
}
