namespace SunCash.Infrastructure.Sync;

/// <summary>Un job di sincronizzazione: importa dati grezzi e dice com'è andata.</summary>
public interface ISyncJob
{
    /// <summary>Nome stabile, chiave di <c>sync_state</c> e di <c>POST /api/sync/{job}/run</c>.</summary>
    string Name { get; }

    /// <summary>Un'esecuzione.</summary>
    Task<SyncOutcome> RunAsync(CancellationToken cancellationToken);
}

/// <summary>Esito di un'esecuzione.</summary>
public enum SyncStatus
{
    /// <summary>Tutto importato.</summary>
    Done,

    /// <summary>Da riprovare più tardi (dati non ancora pubblicati, frequenza eccessiva, job già in corso).</summary>
    RetryLater,

    /// <summary>Errore.</summary>
    Failed,
}

/// <summary>Esito con il messaggio da mostrare nello stato.</summary>
public sealed record SyncOutcome(SyncStatus Status, string? Message = null)
{
    /// <summary>Tutto importato.</summary>
    public static SyncOutcome Done() => new(SyncStatus.Done);

    /// <summary>Da riprovare più tardi.</summary>
    public static SyncOutcome RetryLater(string message) => new(SyncStatus.RetryLater, message);

    /// <summary>Errore.</summary>
    public static SyncOutcome Failed(string message) => new(SyncStatus.Failed, message);
}
