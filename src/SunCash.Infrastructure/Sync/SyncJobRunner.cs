using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SunCash.Infrastructure.Persistence;

namespace SunCash.Infrastructure.Sync;

/// <summary>
/// L'unico punto da cui parte un job, pianificato o manuale: un'esecuzione alla volta per job,
/// esito sempre scritto in <c>sync_state</c>, eccezioni trasformate in errore registrato.
/// </summary>
public sealed class SyncJobRunner(IServiceScopeFactory scopes, ILogger<SyncJobRunner> logger)
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    /// <summary>I nomi dei job registrati.</summary>
    public IReadOnlyList<string> JobNames()
    {
        using var scope = scopes.CreateScope();
        return scope.ServiceProvider.GetServices<ISyncJob>().Select(j => j.Name).ToList();
    }

    /// <summary>Esegue il job; <c>null</c> se non esiste.</summary>
    public async Task<SyncOutcome?> RunAsync(string jobName, CancellationToken ct)
    {
        var gate = _locks.GetOrAdd(jobName, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, ct))
            return SyncOutcome.RetryLater("Il job è già in esecuzione.");

        try
        {
            using var scope = scopes.CreateScope();
            var job = scope.ServiceProvider.GetServices<ISyncJob>().SingleOrDefault(j => j.Name == jobName);
            if (job is null)
                return null;

            var state = scope.ServiceProvider.GetRequiredService<SyncStateStore>();
            SyncOutcome outcome;
            try
            {
                outcome = await job.RunAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Job {Job} fallito", jobName);
                outcome = SyncOutcome.Failed(ex.Message);
            }

            if (outcome.Status == SyncStatus.Done)
                await state.SaveSuccessAsync(jobName, ct);
            else
                await state.SaveErrorAsync(jobName, outcome.Message ?? outcome.Status.ToString(), ct);

            logger.LogInformation("Job {Job}: {Status} {Message}", jobName, outcome.Status, outcome.Message);
            return outcome;
        }
        finally
        {
            gate.Release();
        }
    }
}
