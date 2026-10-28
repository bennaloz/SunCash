using SunCash.Domain.Energy;
using SunCash.Domain.Prices;
using SunCash.Domain.Tariffs;

namespace SunCash.Domain.Valuation;

/// <summary>Tutto quello che serve per valorizzare un giorno italiano.</summary>
/// <param name="Zone">Zona di mercato del prezzo zonale (<c>Valuation:Zone</c>).</param>
/// <param name="PriceFloorEurMwh">Prezzo minimo garantito dell'anno, usato solo con <see cref="ValuationRegime.RidPmg"/>.</param>
/// <param name="PunMonthlyAverageEurMwh">Media mensile del PUN del mese del giorno, per le offerte indicizzate al PUN mensile.</param>
public sealed record ValuationInput(
    DateOnly Day,
    string Zone,
    ValuationRegime Regime,
    decimal? PriceFloorEurMwh,
    IReadOnlyList<EnergyReading> Readings,
    IReadOnlyList<MarketPrice> Prices,
    IReadOnlyList<Tariff> Tariffs,
    IReadOnlyList<RegulatedCharges> RegulatedCharges,
    decimal? PunMonthlyAverageEurMwh);
