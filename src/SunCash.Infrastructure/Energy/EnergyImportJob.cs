using Microsoft.Extensions.Options;
using SunCash.Domain.Energy;
using SunCash.Domain.Time;
using SunCash.Infrastructure.Options;
using SunCash.Infrastructure.Persistence;
using SunCash.Infrastructure.Sync;

namespace SunCash.Infrastructure.Energy;

/// <summary>
/// Job <c>energy-import</c>, uguale per ogni fornitore. A ogni esecuzione chiede al fornitore al massimo
/// <see cref="EnergySourceCapabilities.MaxDaysPerRun"/> giorni, scelti in quest'ordine:
/// <list type="number">
/// <item>ieri, finché non c'è una lettura fatta almeno <see cref="ConsolidationDelay"/> dopo la sua fine
/// (i cloud consolidano in ritardo);</item>
/// <item>oggi, se l'ultima lettura ha più di <see cref="TodayRefresh"/>;</item>
/// <item>lo storico, dal giorno più recente che manca indietro fino a <c>Sync:StartDate</c>;</item>
/// <item>oggi, con quello che avanza.</item>
/// </list>
/// Quali giorni mancano lo dicono le misure salvate (<c>imported_at</c>), non un contatore: così un buco
/// lasciato da un fermo dell'app si richiude da solo. Ogni giorno letto si salva subito. Non nomina mai
/// un fornitore: parla solo con <see cref="IEnergySource"/>.
/// </summary>
public sealed class EnergyImportJob(
    IEnergySource source,
    EnergyReadingStore readings,
    SyncStateStore state,
    IOptions<SyncOptions> sync,
    IOptions<EnergyOptions> energy,
    TimeProvider clock) : ISyncJob
{
    /// <summary>Nome del job.</summary>
    public const string JobName = "energy-import";

    /// <summary>Un giorno passato vale come letto solo se la lettura è di almeno un'ora dopo la sua fine.</summary>
    public static readonly TimeSpan ConsolidationDelay = TimeSpan.FromHours(1);

    /// <summary>Ogni quanto rileggere oggi quando c'è anche storico da recuperare.</summary>
    public static readonly TimeSpan TodayRefresh = TimeSpan.FromHours(3);

    /// <inheritdoc />
    public string Name => JobName;

    /// <inheritdoc />
    public SyncCursorKind CursorKind => SyncCursorKind.CompleteFrom;

    /// <inheritdoc />
    public async Task<SyncOutcome> RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = ItalianTime.LocalDate(now);
        var yesterday = today.AddDays(-1);
        var start = source.Capabilities.CanReadHistory ? sync.Value.StartDate!.Value : yesterday;
        if (start > yesterday)
            start = yesterday;

        var lastImport = await readings.LastImportByDayAsync(source.Id, start, today, ct);
        var budget = source.Capabilities.MaxDaysPerRun ?? 2 + energy.Value.MaxBackfillDaysPerRun;
        var plan = Plan(today, yesterday, start, now, lastImport).Distinct().Take(budget);

        foreach (var day in plan)
        {
            var result = await source.FetchDayAsync(day, ct);
            switch (result.Status)
            {
                case EnergyFetchStatus.Done:
                    await readings.UpsertAsync(result.Readings, ct);
                    break;
                case EnergyFetchStatus.RetryNextCycle:
                    return SyncOutcome.RetryLater($"{day:yyyy-MM-dd}: {result.Message}");
                default:
                    return SyncOutcome.Failed($"{day:yyyy-MM-dd}: {result.Message}");
            }
        }

        await SaveHistoryCursorAsync(start, yesterday, ct);
        return SyncOutcome.Done();
    }

    /// <summary>
    /// Un giorno passato è completo se le sue misure sono state lette almeno <see cref="ConsolidationDelay"/>
    /// dopo la sua fine. Un giorno senza misure non lo è mai: per questo <c>Sync:StartDate</c> non va
    /// messo prima dell'installazione dell'impianto.
    /// </summary>
    private static bool IsComplete(DateOnly day, IReadOnlyDictionary<DateOnly, DateTimeOffset> lastImport) =>
        lastImport.TryGetValue(day, out var at) && at >= ItalianTime.DayBoundsUtc(day).EndUtc + ConsolidationDelay;

    private static IEnumerable<DateOnly> Plan(DateOnly today, DateOnly yesterday, DateOnly start, DateTimeOffset now,
        IReadOnlyDictionary<DateOnly, DateTimeOffset> lastImport)
    {
        if (!IsComplete(yesterday, lastImport))
            yield return yesterday;
        if (!lastImport.TryGetValue(today, out var todayAt) || now - todayAt >= TodayRefresh)
            yield return today;
        for (var day = yesterday.AddDays(-1); day >= start; day = day.AddDays(-1))
            if (!IsComplete(day, lastImport))
                yield return day;
        yield return today;
    }

    // Il cursore dice da quale giorno lo storico è completo fino a ieri: è quello che mostra «Stato».
    private async Task SaveHistoryCursorAsync(DateOnly start, DateOnly yesterday, CancellationToken ct)
    {
        var lastImport = await readings.LastImportByDayAsync(source.Id, start, yesterday, ct);
        DateOnly? from = null;
        for (var day = yesterday; day >= start; day = day.AddDays(-1))
        {
            if (!IsComplete(day, lastImport))
                break;
            from = day;
        }

        if (from is { } f)
            await state.SaveCursorAsync(JobName, f, source.Id, ct);
    }
}
