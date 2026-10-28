namespace SunCash.Domain.Prices;

/// <summary>
/// Media mensile del PUN Index GME, per le offerte indicizzate al PUN mensile: media aritmetica
/// dei valori orari del mese, o di quelli a 15 minuti se gli orari mancano.
/// </summary>
public static class PunMonthlyAverage
{
    /// <summary>La media, o <c>null</c> se non c'è nessun PUN fra i prezzi.</summary>
    public static decimal? Of(IEnumerable<MarketPrice> pricesOfMonth)
    {
        var pun = pricesOfMonth.Where(p => p.Zone == MarketPrice.PunZone).ToList();
        var hourly = pun.Where(p => p.Interval.Minutes == 60).ToList();
        var basis = hourly.Count > 0 ? hourly : pun;
        return basis.Count == 0 ? null : basis.Average(p => p.EurMwh);
    }
}
