using Microsoft.EntityFrameworkCore;
using SunCash.Domain.Tariffs;

namespace SunCash.Infrastructure.Persistence;

/// <summary>
/// Dati con data di validità che il calcolo legge per intero: offerte, parte regolata, prezzi
/// minimi garantiti. Sono poche righe, quindi si leggono tutte e la scelta della riga valida la
/// fa <see cref="Validity"/> nel dominio.
/// </summary>
public sealed class ManualDataStore(SunCashDbContext db)
{
    /// <summary>Tutte le offerte, in ordine di validità.</summary>
    public async Task<IReadOnlyList<Tariff>> TariffsAsync(CancellationToken ct) =>
        (await db.Tariffs.AsNoTracking().OrderBy(t => t.ValidFrom).ToListAsync(ct)).Select(ToDomain).ToList();

    /// <summary>Tutta la parte regolata, in ordine di validità.</summary>
    public async Task<IReadOnlyList<RegulatedCharges>> RegulatedChargesAsync(CancellationToken ct) =>
        (await db.RegulatedCharges.AsNoTracking().OrderBy(c => c.ValidFrom).ToListAsync(ct))
        .Select(c => new RegulatedCharges(c.ValidFrom, c.NetworkEurKwh, c.ExciseEurKwh, c.VatRate)).ToList();

    /// <summary>Prezzi minimi garantiti per anno.</summary>
    public async Task<IReadOnlyDictionary<int, decimal>> PriceFloorsAsync(CancellationToken ct) =>
        await db.PriceFloors.AsNoTracking().ToDictionaryAsync(f => f.Year, f => f.EurMwh, ct);

    /// <summary>Inserisce o sostituisce l'offerta che inizia in <see cref="Tariff.ValidFrom"/>.</summary>
    public async Task SaveTariffAsync(Tariff t, CancellationToken ct)
    {
        var row = await db.Tariffs.SingleOrDefaultAsync(x => x.ValidFrom == t.ValidFrom, ct);
        if (row is null)
            db.Tariffs.Add(row = new TariffRow { ValidFrom = t.ValidFrom });
        row.Kind = t.Kind;
        row.EnergyF1EurKwh = t.EnergyF1EurKwh;
        row.EnergyF2EurKwh = t.EnergyF2EurKwh;
        row.EnergyF3EurKwh = t.EnergyF3EurKwh;
        row.PunSpreadEurKwh = t.PunSpreadEurKwh;
        row.PunBasis = t.PunBasis;
        row.LossesFactor = t.LossesFactor;
        row.Origin = t.Origin;
        row.OfferCode = t.OfferCode;
        row.Notes = t.Notes;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Cancella l'offerta che inizia nella data; falso se non c'era.</summary>
    public async Task<bool> DeleteTariffAsync(DateOnly validFrom, CancellationToken ct) =>
        await db.Tariffs.Where(t => t.ValidFrom == validFrom).ExecuteDeleteAsync(ct) > 0;

    /// <summary>Inserisce o aggiorna righe della parte regolata (caricamento dal repo).</summary>
    public async Task UpsertRegulatedChargesAsync(IEnumerable<RegulatedCharges> charges, CancellationToken ct)
    {
        foreach (var c in charges)
        {
            var row = await db.RegulatedCharges.SingleOrDefaultAsync(x => x.ValidFrom == c.ValidFrom, ct);
            if (row is null)
                db.RegulatedCharges.Add(row = new RegulatedChargesRow { ValidFrom = c.ValidFrom });
            row.NetworkEurKwh = c.NetworkEurKwh;
            row.ExciseEurKwh = c.ExciseEurKwh;
            row.VatRate = c.VatRate;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Inserisce o aggiorna il prezzo minimo garantito di un anno.</summary>
    public async Task UpsertPriceFloorAsync(int year, decimal eurMwh, CancellationToken ct)
    {
        var row = await db.PriceFloors.SingleOrDefaultAsync(x => x.Year == year, ct);
        if (row is null)
            db.PriceFloors.Add(row = new PriceFloorRow { Year = year });
        row.EurMwh = eurMwh;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Tutte le bollette, dalla più recente.</summary>
    public async Task<IReadOnlyList<BillRow>> BillsAsync(CancellationToken ct) =>
        await db.Bills.AsNoTracking().OrderByDescending(b => b.PeriodFrom).ToListAsync(ct);

    /// <summary>Inserisce o sostituisce la bolletta del periodo.</summary>
    public async Task SaveBillAsync(BillRow bill, CancellationToken ct)
    {
        var row = await db.Bills.SingleOrDefaultAsync(x => x.PeriodFrom == bill.PeriodFrom && x.PeriodTo == bill.PeriodTo, ct);
        if (row is null)
            db.Bills.Add(row = new BillRow { PeriodFrom = bill.PeriodFrom, PeriodTo = bill.PeriodTo });
        row.ImportKwh = bill.ImportKwh;
        row.AmountEur = bill.AmountEur;
        row.Origin = bill.Origin;
        row.Notes = bill.Notes;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Cancella la bolletta del periodo; falso se non c'era.</summary>
    public async Task<bool> DeleteBillAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.Bills.Where(b => b.PeriodFrom == from && b.PeriodTo == to).ExecuteDeleteAsync(ct) > 0;

    /// <summary>Pagamenti GSE per mese (<c>yyyy-MM</c>).</summary>
    public async Task<IReadOnlyDictionary<string, GsePaymentRow>> GsePaymentsAsync(CancellationToken ct) =>
        await db.GsePayments.AsNoTracking().ToDictionaryAsync(p => p.Month, ct);

    /// <summary>Inserisce o corregge il pagamento GSE di un mese.</summary>
    public async Task SaveGsePaymentAsync(string month, decimal amountEur, string? notes, CancellationToken ct)
    {
        var row = await db.GsePayments.SingleOrDefaultAsync(x => x.Month == month, ct);
        if (row is null)
            db.GsePayments.Add(row = new GsePaymentRow { Month = month });
        row.AmountEur = amountEur;
        row.Notes = notes;
        await db.SaveChangesAsync(ct);
    }

    private static Tariff ToDomain(TariffRow r) => new(r.ValidFrom, r.Kind, r.EnergyF1EurKwh, r.EnergyF2EurKwh,
        r.EnergyF3EurKwh, r.PunSpreadEurKwh, r.PunBasis, r.LossesFactor, r.Origin, r.OfferCode, r.Notes);
}
