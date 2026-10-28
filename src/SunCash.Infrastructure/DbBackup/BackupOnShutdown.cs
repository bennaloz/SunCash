using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SunCash.Infrastructure.Sync;

namespace SunCash.Infrastructure.DbBackup;

/// <summary>
/// Un backup anche quando l'app si ferma (nuovo deploy, riavvio): dove il disco del database non
/// sopravvive, è ciò che evita di perdere le modifiche fatte dopo il backup notturno.
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
