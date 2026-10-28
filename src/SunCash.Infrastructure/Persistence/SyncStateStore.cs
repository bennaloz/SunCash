using Microsoft.EntityFrameworkCore;

namespace SunCash.Infrastructure.Persistence;

/// <summary>Stato dei job di sincronizzazione (<c>sync_state</c>).</summary>
public sealed class SyncStateStore(SunCashDbContext db, TimeProvider clock)
{
    /// <summary>Lo stato del job, o <c>null</c> se non è mai partito.</summary>
    public Task<SyncStateRow?> GetAsync(string job, CancellationToken ct) =>
        db.SyncState.AsNoTracking().SingleOrDefaultAsync(s => s.Job == job, ct);

    /// <summary>Tutti gli stati.</summary>
    public async Task<IReadOnlyList<SyncStateRow>> ListAsync(CancellationToken ct) =>
        await db.SyncState.AsNoTracking().OrderBy(s => s.Job).ToListAsync(ct);

    /// <summary>Sposta in avanti il cursore dello storico (senza toccare l'esito).</summary>
    public Task SaveCursorAsync(string job, DateOnly cursor, string? source, CancellationToken ct) =>
        UpdateAsync(job, s => { s.Cursor = cursor; s.Source = source ?? s.Source; }, ct);

    /// <summary>Registra un'esecuzione riuscita.</summary>
    public Task SaveSuccessAsync(string job, CancellationToken ct) =>
        UpdateAsync(job, s => { s.LastSuccessUtc = clock.GetUtcNow(); s.LastError = null; }, ct);

    /// <summary>Registra un errore; l'ultimo successo resta com'era.</summary>
    public Task SaveErrorAsync(string job, string error, CancellationToken ct) =>
        UpdateAsync(job, s => s.LastError = error, ct);

    private async Task UpdateAsync(string job, Action<SyncStateRow> change, CancellationToken ct)
    {
        var row = await db.SyncState.SingleOrDefaultAsync(s => s.Job == job, ct);
        if (row is null)
        {
            row = new SyncStateRow { Job = job };
            db.SyncState.Add(row);
        }

        change(row);
        row.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }
}
