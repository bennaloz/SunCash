using Microsoft.Extensions.Options;
using SunCash.Domain.Energy;
using SunCash.Domain.Prices;
using SunCash.Domain.Time;
using SunCash.Domain.Valuation;
using SunCash.Infrastructure.Options;
using SunCash.Infrastructure.Persistence;

namespace SunCash.Infrastructure.Valuation;

/// <summary>Le righe di valorizzazione di un giorno italiano.</summary>
public sealed record ValuedDay(DateOnly Day, IReadOnlyList<ValuationRow> Rows)
{
    /// <summary>Totali del giorno.</summary>
    public ValuationTotals Totals => ValuationTotals.Of(Rows);
}

/// <summary>
/// Legge dal database i dati grezzi di un periodo e li passa a <see cref="ValuationCalculator"/>,
/// un giorno alla volta. Non calcola niente da sé: prepara solo gli ingressi.
/// </summary>
public sealed class ValuationReader(
    EnergyReadingStore readings,
    MarketPriceStore prices,
    ManualDataStore manual,
    IEnergySource activeSource,
    IOptions<ValuationOptions> options)
{
    /// <summary>I giorni italiani da <paramref name="from"/> a <paramref name="to"/>, estremi inclusi.</summary>
    public async Task<IReadOnlyList<ValuedDay>> ForDaysAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (to < from)
            return [];

        var o = options.Value;
        var (start, _) = ItalianTime.DayBoundsUtc(from);
        var (_, end) = ItalianTime.DayBoundsUtc(to);

        // I prezzi si leggono a mesi interi: la media mensile del PUN serve anche per un giorno solo.
        var firstMonth = new DateOnly(from.Year, from.Month, 1);
        var lastMonth = new DateOnly(to.Year, to.Month, 1);
        var (pricesStart, _) = ItalianTime.DayBoundsUtc(firstMonth);
        var (_, pricesEnd) = ItalianTime.DayBoundsUtc(lastMonth.AddMonths(1).AddDays(-1));

        var allReadings = await readings.ListAsync(start, end, ct);
        var allPrices = await prices.ListAsync(pricesStart, pricesEnd, [o.Zone, MarketPrice.PunZone], ct);
        var tariffs = await manual.TariffsAsync(ct);
        var charges = await manual.RegulatedChargesAsync(ct);
        var floors = await manual.PriceFloorsAsync(ct);

        var punByMonth = allPrices
            .GroupBy(p => MonthOf(ItalianTime.LocalDate(p.Interval.StartUtc)))
            .ToDictionary(g => g.Key, g => PunMonthlyAverage.Of(g));

        var days = new List<ValuedDay>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var (dayStart, dayEnd) = ItalianTime.DayBoundsUtc(day);
            var input = new ValuationInput(
                day,
                o.Zone,
                o.Regime,
                floors.TryGetValue(day.Year, out var floor) ? floor : null,
                OneSource(allReadings.Where(r => r.Interval.StartUtc >= dayStart && r.Interval.StartUtc < dayEnd)),
                allPrices.Where(p => p.Interval.StartUtc >= dayStart && p.Interval.StartUtc < dayEnd).ToList(),
                tariffs,
                charges,
                punByMonth.GetValueOrDefault(MonthOf(day)));
            days.Add(new ValuedDay(day, ValuationCalculator.ForDay(input)));
        }

        return days;
    }

    /// <summary>
    /// Misure di un solo fornitore per giorno: quello attivo se le ha, altrimenti quelle che ci sono
    /// (giorni importati prima di un cambio di fornitore). Sommare due fornitori conterebbe due volte.
    /// </summary>
    private IReadOnlyList<EnergyReading> OneSource(IEnumerable<EnergyReading> dayReadings)
    {
        var list = dayReadings.ToList();
        if (list.Any(r => r.Source == activeSource.Id))
            return list.Where(r => r.Source == activeSource.Id).ToList();
        var other = list.Select(r => r.Source).Distinct().Order().FirstOrDefault();
        return list.Where(r => r.Source == other).ToList();
    }

    private static DateOnly MonthOf(DateOnly d) => new(d.Year, d.Month, 1);
}
