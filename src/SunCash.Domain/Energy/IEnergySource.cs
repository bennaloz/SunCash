namespace SunCash.Domain.Energy;

/// <summary>
/// Contratto di un fornitore di dati dell'impianto (FusionSolar, Modbus, …). Tutto ciò che è
/// specifico di un fornitore — login, rate limit, codici d'errore, nomi dei campi — sta dietro
/// questa interfaccia: il job di import, il database e il calcolo non sanno quale fornitore è attivo.
/// </summary>
public interface IEnergySource
{
    /// <summary>Id stabile del fornitore, salvato nella colonna <c>source</c> delle misure.</summary>
    string Id { get; }

    /// <summary>Cosa sa fornire.</summary>
    EnergySourceCapabilities Capabilities { get; }

    /// <summary>Le misure di un giorno in ora italiana.</summary>
    Task<EnergyFetchResult> FetchDayAsync(DateOnly localDate, CancellationToken cancellationToken);
}

/// <summary>Capacità dichiarate da un fornitore.</summary>
/// <param name="ResolutionsMinutes">Granularità delle misure che restituisce (5, 15, 60).</param>
/// <param name="CanReadHistory">Vero se può restituire giorni passati (backfill); falso per le letture solo dal vivo.</param>
public sealed record EnergySourceCapabilities(IReadOnlyList<int> ResolutionsMinutes, bool CanReadHistory);

/// <summary>I soli tre esiti che un fornitore espone al job.</summary>
public enum EnergyFetchStatus
{
    /// <summary>Misure lette (anche zero misure, se il giorno non ne ha).</summary>
    Done,

    /// <summary>Il fornitore chiede di fermarsi fino al prossimo ciclo (es. frequenza eccessiva).</summary>
    RetryNextCycle,

    /// <summary>Errore: il job lo registra e passa oltre.</summary>
    Error,
}

/// <summary>Esito della lettura di un giorno.</summary>
public sealed record EnergyFetchResult(EnergyFetchStatus Status, IReadOnlyList<EnergyReading> Readings, string? Message = null)
{
    /// <summary>Lettura riuscita.</summary>
    public static EnergyFetchResult Done(IReadOnlyList<EnergyReading> readings) => new(EnergyFetchStatus.Done, readings);

    /// <summary>Fermarsi fino al prossimo ciclo.</summary>
    public static EnergyFetchResult RetryNextCycle(string message) => new(EnergyFetchStatus.RetryNextCycle, [], message);

    /// <summary>Errore.</summary>
    public static EnergyFetchResult Error(string message) => new(EnergyFetchStatus.Error, [], message);
}
