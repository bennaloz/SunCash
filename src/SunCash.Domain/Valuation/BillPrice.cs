using SunCash.Domain.Calendar;
using SunCash.Domain.Tariffs;

namespace SunCash.Domain.Valuation;

/// <summary>
/// Prezzo variabile della bolletta in €/kWh, cioè quanto costa un kWh prelevato in più:
/// T = (prezzo energia × (1 + perdite) + rete + accise) × (1 + IVA). Le quote fisse non ci sono
/// perché l'autoconsumo non le riduce.
/// </summary>
public static class BillPrice
{
    /// <summary>
    /// Il prezzo, o <c>null</c> se manca un dato per calcolarlo (offerta, parte regolata, PUN).
    /// </summary>
    /// <param name="punEurMwh">PUN Index GME dell'intervallo, per le offerte indicizzate al PUN orario.</param>
    /// <param name="punMonthlyAverageEurMwh">Media mensile del PUN, per le offerte indicizzate al PUN mensile.</param>
    public static decimal? EurKwh(
        Tariff? tariff,
        RegulatedCharges? charges,
        TariffBand band,
        decimal? punEurMwh,
        decimal? punMonthlyAverageEurMwh)
    {
        if (tariff is null || charges is null)
            return null;

        var energy = EnergyEurKwh(tariff, band, punEurMwh, punMonthlyAverageEurMwh);
        if (energy is null)
            return null;

        return (energy.Value * (1 + tariff.LossesFactor) + charges.NetworkEurKwh + charges.ExciseEurKwh)
               * (1 + charges.VatRate);
    }

    private static decimal? EnergyEurKwh(Tariff tariff, TariffBand band, decimal? pun, decimal? punMonthly) =>
        tariff.Kind switch
        {
            OfferKind.Monoraria => tariff.EnergyF1EurKwh,
            OfferKind.Fasce => band switch
            {
                TariffBand.F1 => tariff.EnergyF1EurKwh,
                TariffBand.F2 => tariff.EnergyF2EurKwh,
                _ => tariff.EnergyF3EurKwh,
            },
            OfferKind.IndicizzataPun =>
                (tariff.PunBasis == PunBasis.Mensile ? punMonthly : pun) is { } p && tariff.PunSpreadEurKwh is { } spread
                    ? p / 1000m + spread
                    : null,
            _ => null,
        };
}
