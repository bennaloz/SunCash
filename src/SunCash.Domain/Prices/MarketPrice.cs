using SunCash.Domain.Time;

namespace SunCash.Domain.Prices;

/// <summary>Un prezzo di mercato del GME su un intervallo (15 o 60 minuti), in €/MWh.</summary>
public sealed record MarketPrice(Interval Interval, string Market, string Zone, decimal EurMwh)
{
    /// <summary>Il mercato del giorno prima, l'unico che SunCash usa.</summary>
    public const string Mgp = "MGP";

    /// <summary>La «zona» con cui il GME pubblica il PUN Index GME fra i prezzi zonali MGP.</summary>
    public const string PunZone = "PUN";
}
