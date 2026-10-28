using System.Globalization;
using SunCash.Domain.Prices;
using SunCash.Domain.Time;

namespace SunCash.Infrastructure.Gme;

/// <summary>
/// Trasforma gli elementi GME in prezzi con l'istante UTC. L'ora non si interpreta: si conta.
/// <c>Period</c> (o <c>Hour</c> per i dati orari) è la posizione nel giorno italiano, quindi i giorni
/// da 92 e 100 quarti d'ora tornano da soli.
/// </summary>
public static class GmePriceMapper
{
    /// <summary>Prezzi delle zone richieste, alla granularità indicata (15 o 60 minuti).</summary>
    public static IReadOnlyList<MarketPrice> ToMarketPrices(
        IEnumerable<GmeZonalPriceRecord> records, int minutes, IReadOnlyCollection<string> zones)
    {
        var prices = new List<MarketPrice>();
        foreach (var day in records.Where(r => r.Price is not null && zones.Contains(r.Zone)).GroupBy(r => r.FlowDate))
        {
            var date = DateOnly.ParseExact(day.Key.ToString(CultureInfo.InvariantCulture), "yyyyMMdd", CultureInfo.InvariantCulture);
            var quarters = ItalianTime.QuarterStartsUtc(date);
            foreach (var r in day)
            {
                var index = minutes == 15 ? (r.Period ?? 0) - 1 : (r.Hour - 1) * 4;
                if (index < 0 || index >= quarters.Count)
                    throw new GmeException($"Posizione fuori dal giorno {date}: Hour {r.Hour}, Period {r.Period}.");
                prices.Add(new MarketPrice(new Interval(quarters[index], minutes), r.Market, r.Zone, r.Price!.Value));
            }
        }

        return prices;
    }
}
