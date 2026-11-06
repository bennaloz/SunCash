using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SunCash.Domain.Time;

namespace SunCash.Infrastructure.Sync;

/// <summary>Quando gira un job: un'espressione cron in ora italiana e, se serve, i tentativi successivi.</summary>
/// <param name="Cron">Espressione cron a 5 campi, valutata in <c>Europe/Rome</c>.</param>
/// <param name="RetryInterval">Se l'esito non è <see cref="SyncStatus.Done"/>, riprova dopo questo
/// intervallo fino alla prossima esecuzione pianificata; <c>null</c> = aspetta il prossimo ciclo.</param>
public sealed record SyncSchedule(string JobName, string Cron, TimeSpan? RetryInterval);

/// <summary>Fa girare un job secondo la sua pianificazione, più una volta all'avvio (backfill).</summary>
public sealed class SyncScheduler(SyncSchedule schedule, SyncJobRunner runner, TimeProvider clock, ILogger<SyncScheduler> logger)
    : BackgroundService
{
    private readonly CronExpression _cron = CronExpression.Parse(schedule.Cron);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTimeOffset? waitedFor = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = NextOccurrence(_cron, clock.GetUtcNow(), waitedFor)
                       ?? throw new InvalidOperationException($"Il cron di {schedule.JobName} non ha prossime esecuzioni.");

            var outcome = await runner.RunAsync(schedule.JobName, stoppingToken);
            while (outcome?.Status is not (null or SyncStatus.Done) && schedule.RetryInterval is { } retry &&
                   clock.GetUtcNow() + retry < next)
            {
                await Task.Delay(retry, clock, stoppingToken);
                outcome = await runner.RunAsync(schedule.JobName, stoppingToken);
            }

            var wait = next - clock.GetUtcNow();
            logger.LogDebug("Job {Job}: prossima esecuzione {Next}", schedule.JobName, next);
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, clock, stoppingToken);
            waitedFor = next;
        }
    }

    /// <summary>
    /// La prossima esecuzione, mai prima di quella appena attesa. Il timer può svegliarsi qualche
    /// millisecondo prima dell'ora: calcolando da «adesso» si otterrebbe di nuovo la stessa ora e il
    /// job girerebbe due volte di fila. È successo il 2026-11-06: ogni ora una seconda chiamata a
    /// FusionSolar, puntualmente bloccata per frequenza eccessiva.
    /// </summary>
    public static DateTimeOffset? NextOccurrence(CronExpression cron, DateTimeOffset now, DateTimeOffset? waitedFor) =>
        cron.GetNextOccurrence(waitedFor is { } w && w > now ? w : now, ItalianTime.Zone);
}
