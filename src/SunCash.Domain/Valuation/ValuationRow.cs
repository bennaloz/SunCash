using SunCash.Domain.Calendar;
using SunCash.Domain.Time;

namespace SunCash.Domain.Valuation;

/// <summary>
/// Una riga di valorizzazione: un quarto d'ora, o un'ora intera se c'è solo il dato orario.
/// Gli euro non si arrotondano qui: si arrotonda solo in presentazione.
/// </summary>
public sealed record ValuationRow(
    Interval Interval,
    Resolution Resolution,
    DateOnly LocalDate,
    string LocalLabel,
    TariffBand Band,
    long? ExportWh,
    decimal? ZonalEurMwh,
    decimal? PunEurMwh,
    decimal? ExportValueEur,
    decimal? FloorValueEur,
    long? SelfConsumptionWh,
    SelfConsumptionBasis SelfConsumptionBasis,
    decimal? BillPriceEurKwh,
    decimal? SavingsEur);
