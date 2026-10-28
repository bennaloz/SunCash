namespace SunCash.Domain.Valuation;

/// <summary>
/// Totali di un insieme di righe di valorizzazione (un'ora, un giorno, un mese, un anno). Tutte le
/// aggregazioni passano da qui, così ore, giorni e mesi sono la stessa somma.
/// </summary>
public sealed record ValuationTotals(
    long ExportWh,
    long SelfConsumptionWh,
    decimal ExportValueEur,
    decimal SavingsEur,
    int RowsWithoutExport,
    int RowsWithoutSavings,
    long ImportWh,
    decimal ImportCostEur,
    decimal? AverageZonalEurMwh,
    long ValuedExportWh)
{
    /// <summary>Beneficio totale: valore dell'immesso più risparmio.</summary>
    public decimal BenefitEur => ExportValueEur + SavingsEur;

    /// <summary>
    /// Prezzo medio ponderato catturato dall'immesso in €/MWh (valore / energia), contando solo
    /// l'energia che ha un valore, o <c>null</c> se non ce n'è: senza prezzi il prezzo catturato è
    /// sconosciuto, non zero.
    /// </summary>
    public decimal? CapturedPriceEurMwh => ValuedExportWh == 0 ? null : ExportValueEur / (ValuedExportWh / 1_000_000m);

    /// <summary>
    /// Somma le righe. La media del prezzo zonale è pesata sulla durata delle righe, così un'ora
    /// valorizzata come blocco unico pesa quanto quattro quarti d'ora: è il termine di confronto del
    /// prezzo catturato. Le righe senza dato contano come zero e si contano a parte, così
    /// la dashboard può dire che un totale è incompleto.
    /// </summary>
    public static ValuationTotals Of(IEnumerable<ValuationRow> rows)
    {
        long export = 0, valuedExport = 0, self = 0, import = 0, pricedMinutes = 0;
        decimal value = 0, savings = 0, importCost = 0, priceByMinutes = 0;
        int noExport = 0, noSavings = 0;
        foreach (var r in rows)
        {
            export += r.ExportWh ?? 0;
            self += r.SelfConsumptionWh ?? 0;
            if (r.ExportValueEur is { } v) { value += v; valuedExport += r.ExportWh ?? 0; } else noExport++;
            if (r.SavingsEur is { } s) savings += s; else noSavings++;
            import += r.ImportWh ?? 0;
            importCost += r.ImportCostEur ?? 0;
            if (r.ZonalEurMwh is { } z)
            {
                priceByMinutes += z * r.Interval.Minutes;
                pricedMinutes += r.Interval.Minutes;
            }
        }

        return new ValuationTotals(export, self, value, savings, noExport, noSavings, import, importCost,
            pricedMinutes == 0 ? null : priceByMinutes / pricedMinutes, valuedExport);
    }
}
