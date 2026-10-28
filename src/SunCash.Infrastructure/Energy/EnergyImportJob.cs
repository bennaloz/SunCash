using Microsoft.Extensions.Options;
using SunCash.Domain.Energy;
using SunCash.Domain.Time;
using SunCash.Infrastructure.Options;
using SunCash.Infrastructure.Persistence;
using SunCash.Infrastructure.Sync;

namespace SunCash.Infrastructure.Energy;

/// <summary>
/// Job <c>energy-import</c>, uguale per ogni fornitore: rilegge ieri e oggi (i cloud consolidano in
/// ritardo), poi avanza nello storico un giorno alla volta. Non nomina mai un fornitore: parla solo
/// con <see cref="IEnergySource"/>.
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

    /// <inheritdoc />
    public string Name => JobName;

    /// <inheritdoc />
    public async Task<SyncOutcome> RunAsync(CancellationToken ct)
    {
        var today = ItalianTime.LocalDate(clock.GetUtcNow());
        var yesterday = today.AddDays(-1);

        foreach (var day in new[] { yesterday, today })
            if (await ImportAsync(day, ct) is { } stop)
                return stop;

        if (!source.Capabilities.CanReadHistory)
            return SyncOutcome.Done();

        var cursor = (await state.GetAsync(JobName, ct))?.Cursor ?? sync.Value.StartDate!.Value;
        for (var n = 0; cursor < yesterday && n < energy.Value.MaxBackfillDaysPerRun; n++)
        {
            if (await ImportAsync(cursor, ct) is { } stop)
                return stop;
            cursor = cursor.AddDays(1);
            await state.SaveCursorAsync(JobName, cursor, source.Id, ct);
        }

        return cursor < yesterday
            ? SyncOutcome.RetryLater($"Storico in ricostruzione: completo fino al {cursor.AddDays(-1):yyyy-MM-dd}.")
            : SyncOutcome.Done();
    }

    /// <summary>Importa un giorno; restituisce l'esito con cui fermarsi, o <c>null</c> per continuare.</summary>
    private async Task<SyncOutcome?> ImportAsync(DateOnly day, CancellationToken ct)
    {
        var result = await source.FetchDayAsync(day, ct);
        switch (result.Status)
        {
            case EnergyFetchStatus.Done:
                await readings.UpsertAsync(result.Readings, ct);
                return null;
            case EnergyFetchStatus.RetryNextCycle:
                return SyncOutcome.RetryLater($"{day:yyyy-MM-dd}: {result.Message}");
            default:
                return SyncOutcome.Failed($"{day:yyyy-MM-dd}: {result.Message}");
        }
    }
}
