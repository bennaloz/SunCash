using SunCash.Domain.Calendar;
using SunCash.Domain.Tariffs;
using SunCash.Domain.Valuation;

namespace SunCash.Api.Endpoints;

/// <summary>
/// Una riga di valorizzazione come la vede il frontend: istante UTC e etichetta italiana già pronta
/// (il frontend non converte fusi), energia in kWh. Gli euro non sono arrotondati: lo fa la UI.
/// </summary>
public sealed record ValuationRowDto(
    DateTimeOffset StartUtc,
    int Minutes,
    DateOnly LocalDate,
    string LocalLabel,
    Resolution Resolution,
    TariffBand Band,
    decimal? ExportKwh,
    decimal? ZonalEurMwh,
    decimal? PunEurMwh,
    decimal? ExportValueEur,
    decimal? SelfConsumptionKwh,
    SelfConsumptionBasis SelfConsumptionBasis,
    decimal? BillPriceEurKwh,
    decimal? SavingsEur,
    decimal? ImportKwh,
    decimal? ImportCostEur)
{
    /// <summary>Dalla riga di dominio.</summary>
    public static ValuationRowDto From(ValuationRow r) => new(
        r.Interval.StartUtc, r.Interval.Minutes, r.LocalDate, r.LocalLabel, r.Resolution, r.Band,
        Kwh(r.ExportWh), r.ZonalEurMwh, r.PunEurMwh, r.ExportValueEur, Kwh(r.SelfConsumptionWh),
        r.SelfConsumptionBasis, r.BillPriceEurKwh, r.SavingsEur, Kwh(r.ImportWh), r.ImportCostEur);

    internal static decimal? Kwh(long? wh) => wh / 1000m;
}

/// <summary>Totali di un periodo (ora, giorno, mese, anno).</summary>
public sealed record TotalsDto(
    decimal ExportKwh,
    decimal SelfConsumptionKwh,
    decimal ImportKwh,
    decimal ExportValueEur,
    decimal SavingsEur,
    decimal BenefitEur,
    decimal ImportCostEur,
    decimal? CapturedPriceEurMwh,
    bool Incomplete,
    bool SavingsUnavailable)
{
    /// <summary>Dai totali di dominio.</summary>
    public static TotalsDto From(ValuationTotals t) => new(
        t.ExportWh / 1000m, t.SelfConsumptionWh / 1000m, t.ImportWh / 1000m, t.ExportValueEur, t.SavingsEur,
        t.BenefitEur, t.ImportCostEur, t.CapturedPriceEurMwh, t.RowsWithoutExport > 0, t.RowsWithoutSavings > 0);
}

/// <summary>Totali con l'etichetta del periodo.</summary>
public sealed record PeriodTotalsDto(string Period, DateTimeOffset? StartUtc, TotalsDto Totals);

/// <summary>Totali di un mese con il pagamento GSE inserito a mano, se c'è.</summary>
public sealed record MonthTotalsDto(string Month, TotalsDto Totals, decimal? GsePaymentEur);

/// <summary>Riepilogo della dashboard.</summary>
/// <param name="ExportValueRecognizedEur">Valore dell'immesso riconosciuto nell'anno secondo il regime (con il PMG, la serie più favorevole).</param>
/// <param name="AnnualFixedCostsEur">Costi fissi annui: si mostrano solo nel cumulato.</param>
public sealed record SummaryDto(
    TotalsDto Today,
    TotalsDto Yesterday,
    TotalsDto Month,
    TotalsDto Year,
    PeriodTotalsDto? BestDay,
    ValuationRegime Regime,
    decimal ExportValueRecognizedEur,
    decimal AnnualFixedCostsEur);

/// <summary>Un'offerta luce in ingresso e in uscita.</summary>
public sealed record TariffDto(
    DateOnly ValidFrom,
    OfferKind Kind,
    decimal? EnergyF1EurKwh,
    decimal? EnergyF2EurKwh,
    decimal? EnergyF3EurKwh,
    decimal? PunSpreadEurKwh,
    PunBasis? PunBasis,
    decimal LossesFactor,
    DataOrigin Origin,
    string? OfferCode,
    string? Notes)
{
    /// <summary>Dal dominio.</summary>
    public static TariffDto From(Tariff t) => new(t.ValidFrom, t.Kind, t.EnergyF1EurKwh, t.EnergyF2EurKwh,
        t.EnergyF3EurKwh, t.PunSpreadEurKwh, t.PunBasis, t.LossesFactor, t.Origin, t.OfferCode, t.Notes);

    /// <summary>Verso il dominio.</summary>
    public Tariff ToDomain() => new(ValidFrom, Kind, EnergyF1EurKwh, EnergyF2EurKwh, EnergyF3EurKwh,
        PunSpreadEurKwh, PunBasis, LossesFactor, Origin, OfferCode, Notes);

    /// <summary>Gli errori di compilazione del modulo, per campo; vuoto se va bene.</summary>
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        void Require(decimal? value, string field)
        {
            if (value is null or < 0)
                errors[field] = ["Serve un prezzo in €/kWh, zero o positivo."];
        }

        switch (Kind)
        {
            case OfferKind.Monoraria:
                Require(EnergyF1EurKwh, nameof(EnergyF1EurKwh));
                break;
            case OfferKind.Fasce:
                Require(EnergyF1EurKwh, nameof(EnergyF1EurKwh));
                Require(EnergyF2EurKwh, nameof(EnergyF2EurKwh));
                Require(EnergyF3EurKwh, nameof(EnergyF3EurKwh));
                break;
            case OfferKind.IndicizzataPun:
                Require(PunSpreadEurKwh, nameof(PunSpreadEurKwh));
                if (PunBasis is null)
                    errors[nameof(PunBasis)] = ["Indica se l'offerta segue il PUN orario o la media mensile."];
                break;
        }

        if (LossesFactor is < 0 or > 1)
            errors[nameof(LossesFactor)] = ["Le perdite sono una frazione fra 0 e 1 (es. 0,102)."];
        return errors;
    }
}

/// <summary>Una bolletta reale con accanto i numeri stimati da SunCash per lo stesso periodo.</summary>
/// <param name="EstimatedImportKwh">Prelievi misurati dall'impianto nel periodo.</param>
/// <param name="EstimatedVariableCostEur">Costo variabile stimato dei prelievi (senza quote fisse).</param>
public sealed record BillDto(
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    decimal? ImportKwh,
    decimal AmountEur,
    DataOrigin Origin,
    string? Notes,
    decimal? EstimatedImportKwh,
    decimal? EstimatedVariableCostEur);

/// <summary>Una bolletta in ingresso.</summary>
public sealed record BillInput(decimal? ImportKwh, decimal AmountEur, string? Notes);

/// <summary>Un pagamento GSE in ingresso.</summary>
public sealed record GsePaymentInput(decimal AmountEur, string? Notes);

/// <summary>Stato di un job.</summary>
public sealed record SyncJobStatusDto(string Job, string? Source, DateTimeOffset? LastSuccessUtc, DateOnly? Cursor, string? LastError, bool Stale);

/// <summary>Stato complessivo della sincronizzazione.</summary>
/// <param name="DaysWithGaps">Giorni recenti con quarti d'ora senza energia o senza prezzo.</param>
public sealed record SyncStatusDto(string ActiveSource, IReadOnlyList<SyncJobStatusDto> Jobs, IReadOnlyList<DateOnly> DaysWithGaps, bool RegulatedChargesMissing);
