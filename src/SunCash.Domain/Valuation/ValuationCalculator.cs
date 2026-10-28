using SunCash.Domain.Calendar;
using SunCash.Domain.Energy;
using SunCash.Domain.Prices;
using SunCash.Domain.Tariffs;
using SunCash.Domain.Time;

namespace SunCash.Domain.Valuation;

/// <summary>
/// Calcolatore di riferimento del valore dell'energia immessa e del risparmio da autoconsumo
/// (docs/analisi.md, «Regole di calcolo»). Le view del database applicano le stesse regole e i
/// loro totali devono coincidere con questi.
/// </summary>
public static class ValuationCalculator
{
    /// <summary>
    /// Le righe di un giorno italiano. Per ogni ora: quattro righe a 15 minuti se i dati fini
    /// coprono tutti i quarti d'ora, altrimenti una riga oraria se c'è il dato orario, altrimenti
    /// quattro righe a 15 minuti senza energia (il buco si vede invece di sparire).
    /// </summary>
    public static IReadOnlyList<ValuationRow> ForDay(ValuationInput input)
    {
        var tariff = Validity.At(input.Tariffs, t => t.ValidFrom, input.Day);
        var charges = Validity.At(input.RegulatedCharges, c => c.ValidFrom, input.Day);
        var rows = new List<ValuationRow>(100);

        foreach (var hour in TimeSlotCalendar.ForDay(input.Day).GroupBy(s => ItalianTime.HourStartUtc(s.StartUtc)))
        {
            var slots = hour.ToList();
            var fine = slots.Select(s => FineReadings(input.Readings, s.Interval)).ToList();
            var hourly = input.Readings.Where(r => r.Interval.Minutes == 60 && r.Interval.StartUtc == hour.Key).ToList();

            if (fine.All(Covers) || hourly.Count == 0)
            {
                for (var i = 0; i < slots.Count; i++)
                {
                    var totals = Covers(fine[i]) ? EnergyTotals.Sum(fine[i]) : null;
                    rows.Add(Row(input, tariff, charges, slots[i], slots[i].Interval, Resolution.Quarter, totals));
                }
            }
            else
            {
                rows.Add(Row(input, tariff, charges, slots[0], new Interval(hour.Key, 60), Resolution.Hour, EnergyTotals.Sum(hourly)));
            }
        }

        return rows;
    }

    /// <summary>
    /// Valore dell'immesso riconosciuto nell'anno. Con <see cref="ValuationRegime.RidPmg"/> vale la
    /// serie più favorevole fra prezzo zonale e prezzo minimo garantito.
    /// DA VERIFICARE (docs/analisi.md, «Regime di valorizzazione»): il confronto sull'anno intero è
    /// un'ipotesi da confermare sulla convenzione GSE.
    /// </summary>
    public static decimal ExportValueForYear(ValuationRegime regime, IEnumerable<ValuationRow> rowsOfYear)
    {
        var rows = rowsOfYear.ToList();
        var zonal = rows.Sum(r => r.ExportValueEur ?? 0);
        return regime == ValuationRegime.RidPmg ? Math.Max(zonal, rows.Sum(r => r.FloorValueEur ?? 0)) : zonal;
    }

    private static ValuationRow Row(
        ValuationInput input,
        Tariff? tariff,
        RegulatedCharges? charges,
        TimeSlot firstSlot,
        Interval interval,
        Resolution resolution,
        EnergyTotals? energy)
    {
        var zonal = Price(input.Prices, input.Zone, interval);
        var pun = Price(input.Prices, MarketPrice.PunZone, interval);
        var exportWh = energy?.ExportWh;

        var (selfWh, basis) = energy is null ? (null, SelfConsumptionBasis.Unavailable) : SelfConsumption.Compute(energy);
        var billPrice = BillPrice.EurKwh(tariff, charges, firstSlot.Band, pun, input.PunMonthlyAverageEurMwh);

        return new ValuationRow(
            interval,
            resolution,
            firstSlot.LocalDate,
            firstSlot.LocalLabel,
            firstSlot.Band,
            exportWh,
            zonal,
            pun,
            exportWh is { } e && zonal is { } z ? e / 1_000_000m * z : null,
            input.Regime == ValuationRegime.RidPmg && exportWh is { } e2 && input.PriceFloorEurMwh is { } floor
                ? e2 / 1_000_000m * floor
                : null,
            selfWh,
            basis,
            billPrice,
            selfWh is { } s && billPrice is { } t ? s / 1000m * t : null,
            energy?.ImportWh,
            energy?.ImportWh is { } imp && billPrice is { } t2 ? imp / 1000m * t2 : null);
    }

    /// <summary>Prezzo MGP della zona per l'intervallo: a 15 minuti se c'è e l'intervallo è un
    /// quarto d'ora, altrimenti il prezzo orario dell'ora che lo contiene.</summary>
    private static decimal? Price(IReadOnlyList<MarketPrice> prices, string zone, Interval interval)
    {
        var mgp = prices.Where(p => p.Market == MarketPrice.Mgp && p.Zone == zone);
        if (interval.Minutes == ItalianTime.QuarterMinutes &&
            mgp.FirstOrDefault(p => p.Interval.Minutes == 15 && p.Interval.StartUtc == interval.StartUtc) is { } quarter)
            return quarter.EurMwh;

        var hourStart = ItalianTime.HourStartUtc(interval.StartUtc);
        return mgp.FirstOrDefault(p => p.Interval.Minutes == 60 && p.Interval.StartUtc == hourStart)?.EurMwh;
    }

    /// <summary>Misure a 5 o 15 minuti che cadono nel quarto d'ora.</summary>
    private static List<EnergyReading> FineReadings(IReadOnlyList<EnergyReading> readings, Interval quarter) =>
        readings.Where(r => r.Interval.Minutes is 5 or 15 && quarter.Contains(r.Interval.StartUtc)).ToList();

    /// <summary>Vero se le misure fini coprono esattamente i 15 minuti del quarto d'ora.</summary>
    private static bool Covers(List<EnergyReading> fine) =>
        fine.Sum(r => r.Interval.Minutes) == ItalianTime.QuarterMinutes &&
        fine.Select(r => r.Interval.StartUtc).Distinct().Count() == fine.Count;
}
