using SunCash.Domain.Calendar;
using SunCash.Domain.Energy;
using SunCash.Domain.Prices;
using SunCash.Domain.Tariffs;
using SunCash.Domain.Time;
using SunCash.Domain.Valuation;

namespace SunCash.Domain.Tests;

public class ValuationCalculatorTests
{
    private const string Zone = "NORD";
    private static readonly DateOnly Day = new(2026, 6, 17); // mercoledì, ora legale

    /// <summary>Le 12:00 italiane del giorno di prova, cioè le 10:00 UTC.</summary>
    private static readonly DateTimeOffset Noon = new(2026, 6, 17, 10, 0, 0, TimeSpan.Zero);

    private static readonly RegulatedCharges Charges = new(new DateOnly(2026, 1, 1), 0.04m, 0.0227m, 0.10m);

    [Theory]
    [InlineData("2026-03-29", 92)]
    [InlineData("2026-06-17", 96)]
    [InlineData("2026-10-25", 100)]
    public void Senza_dati_orari_un_giorno_ha_una_riga_per_quarto_d_ora(string day, int expected)
    {
        var rows = ValuationCalculator.ForDay(Input(DateOnly.Parse(day), [], []));

        Assert.Equal(expected, rows.Count);
        Assert.All(rows, r => Assert.Equal(Resolution.Quarter, r.Resolution));
    }

    [Fact]
    public void Tre_misure_a_5_minuti_fanno_un_quarto_d_ora_al_prezzo_del_quarto_d_ora()
    {
        var readings = Enumerable.Range(0, 3)
            .Select(i => Reading(Noon.AddMinutes(5 * i), 5, export: 100))
            .ToList();
        var prices = new List<MarketPrice> { ZonalPrice(Noon, 15, 120m), ZonalPrice(Noon, 60, 999m) };

        var row = ValuationCalculator.ForDay(Input(Day, readings, prices)).Single(r => r.Interval.StartUtc == Noon);

        Assert.Equal(Resolution.Quarter, row.Resolution);
        Assert.Equal(300, row.ExportWh);
        Assert.Equal(120m, row.ZonalEurMwh);
        Assert.Equal(300 / 1_000_000m * 120m, row.ExportValueEur);
    }

    [Fact]
    public void Senza_prezzo_a_15_minuti_il_quarto_d_ora_ricade_sul_prezzo_orario()
    {
        var readings = new List<EnergyReading> { Reading(Noon.AddMinutes(15), 15, export: 500) };
        var prices = new List<MarketPrice> { ZonalPrice(Noon, 60, 80m) };

        var row = ValuationCalculator.ForDay(Input(Day, readings, prices)).Single(r => r.Interval.StartUtc == Noon.AddMinutes(15));

        Assert.Equal(80m, row.ZonalEurMwh);
        Assert.Equal(500 / 1_000_000m * 80m, row.ExportValueEur);
    }

    [Fact]
    public void Con_il_solo_dato_orario_l_ora_vale_come_blocco_unico_al_prezzo_orario()
    {
        var readings = new List<EnergyReading> { Reading(Noon, 60, export: 2000) };
        var prices = new List<MarketPrice> { ZonalPrice(Noon, 15, 1m), ZonalPrice(Noon, 60, 100m) };

        var rows = ValuationCalculator.ForDay(Input(Day, readings, prices));
        var row = rows.Single(r => r.Interval.StartUtc == Noon);

        Assert.Equal(93, rows.Count); // 96 quarti, meno 4, più una riga oraria
        Assert.Equal(Resolution.Hour, row.Resolution);
        Assert.Equal(60, row.Interval.Minutes);
        Assert.Equal("12:00", row.LocalLabel);
        Assert.Equal(2000 / 1_000_000m * 100m, row.ExportValueEur);
    }

    [Fact]
    public void Se_i_dati_fini_coprono_solo_parte_dell_ora_si_usa_il_dato_orario()
    {
        var readings = new List<EnergyReading>
        {
            Reading(Noon, 15, export: 100),
            Reading(Noon, 60, export: 2000),
        };

        var row = ValuationCalculator.ForDay(Input(Day, readings, [ZonalPrice(Noon, 60, 100m)]))
            .Single(r => r.Interval.StartUtc == Noon);

        Assert.Equal(Resolution.Hour, row.Resolution);
        Assert.Equal(2000, row.ExportWh);
    }

    [Fact]
    public void Il_PUN_si_legge_ma_non_entra_nel_valore_dell_immesso()
    {
        var readings = new List<EnergyReading> { Reading(Noon, 15, export: 1000) };
        var prices = new List<MarketPrice>
        {
            ZonalPrice(Noon, 15, 100m),
            new(new Interval(Noon, 15), MarketPrice.Mgp, MarketPrice.PunZone, 200m),
        };

        var row = ValuationCalculator.ForDay(Input(Day, readings, prices)).Single(r => r.Interval.StartUtc == Noon);

        Assert.Equal(200m, row.PunEurMwh);
        Assert.Equal(1000 / 1_000_000m * 100m, row.ExportValueEur);
    }

    [Fact]
    public void Autoconsumo_dal_consumo_misurato()
    {
        var reading = Reading(Noon, 15, export: 0, import: 200, consumption: 1000);

        var row = ValuationCalculator.ForDay(Input(Day, [reading], [], Monoraria(0.10m))).Single(r => r.Interval.StartUtc == Noon);

        Assert.Equal(SelfConsumptionBasis.Measured, row.SelfConsumptionBasis);
        Assert.Equal(800, row.SelfConsumptionWh);
        var expectedPrice = (0.10m + 0.04m + 0.0227m) * 1.10m;
        Assert.Equal(expectedPrice, row.BillPriceEurKwh);
        Assert.Equal(0.8m * expectedPrice, row.SavingsEur);
    }

    [Fact]
    public void Autoconsumo_dal_bilancio_con_la_batteria()
    {
        // Sera: niente produzione, la batteria scarica 600 Wh, la casa ne preleva 100 dalla rete.
        var reading = Reading(Noon, 15, export: 0, import: 100, production: 0, charge: 0, discharge: 600);

        var row = ValuationCalculator.ForDay(Input(Day, [reading], [], Monoraria(0.10m))).Single(r => r.Interval.StartUtc == Noon);

        Assert.Equal(SelfConsumptionBasis.Balance, row.SelfConsumptionBasis);
        Assert.Equal(600, row.SelfConsumptionWh);
    }

    [Fact]
    public void La_carica_della_batteria_non_e_autoconsumo()
    {
        // Mezzogiorno: produce 1000 Wh, ne immette 100, ne mette 700 in batteria; la casa ne usa 200.
        var reading = Reading(Noon, 15, export: 100, import: 0, production: 1000, charge: 700, discharge: 0);

        var row = ValuationCalculator.ForDay(Input(Day, [reading], [], Monoraria(0.10m))).Single(r => r.Interval.StartUtc == Noon);

        Assert.Equal(200, row.SelfConsumptionWh);
    }

    [Fact]
    public void Senza_consumo_ne_batteria_il_risparmio_non_si_calcola()
    {
        var reading = Reading(Noon, 15, export: 100, import: 0, production: 1000);

        var row = ValuationCalculator.ForDay(Input(Day, [reading], [], Monoraria(0.10m))).Single(r => r.Interval.StartUtc == Noon);

        Assert.Equal(SelfConsumptionBasis.Unavailable, row.SelfConsumptionBasis);
        Assert.Null(row.SelfConsumptionWh);
        Assert.Null(row.SavingsEur);
    }

    [Fact]
    public void Offerta_a_fasce_usa_il_prezzo_della_fascia_del_quarto_d_ora()
    {
        var tariff = new Tariff(new DateOnly(2026, 1, 1), OfferKind.Fasce, 0.30m, 0.20m, 0.10m, null, null, 0m, DataOrigin.Manual);
        var evening = new DateTimeOffset(2026, 6, 17, 21, 30, 0, TimeSpan.Zero); // 23:30 italiane: F3

        var rows = ValuationCalculator.ForDay(Input(Day, [], [], tariff));

        Assert.Equal(Price(0.30m), rows.Single(r => r.Interval.StartUtc == Noon).BillPriceEurKwh);
        Assert.Equal(Price(0.10m), rows.Single(r => r.Interval.StartUtc == evening).BillPriceEurKwh);
    }

    [Fact]
    public void Offerta_indicizzata_al_PUN_orario_con_perdite()
    {
        var tariff = new Tariff(new DateOnly(2026, 1, 1), OfferKind.IndicizzataPun, null, null, null, 0.02m, PunBasis.Orario, 0.102m, DataOrigin.Manual);
        var prices = new List<MarketPrice> { new(new Interval(Noon, 15), MarketPrice.Mgp, MarketPrice.PunZone, 110m) };

        var row = ValuationCalculator.ForDay(Input(Day, [], prices, tariff)).Single(r => r.Interval.StartUtc == Noon);

        Assert.Equal(((0.110m + 0.02m) * 1.102m + 0.04m + 0.0227m) * 1.10m, row.BillPriceEurKwh);
    }

    [Fact]
    public void Offerta_indicizzata_al_PUN_mensile_usa_la_media_del_mese()
    {
        var tariff = new Tariff(new DateOnly(2026, 1, 1), OfferKind.IndicizzataPun, null, null, null, 0.02m, PunBasis.Mensile, 0m, DataOrigin.Manual);
        var prices = new List<MarketPrice> { new(new Interval(Noon, 15), MarketPrice.Mgp, MarketPrice.PunZone, 999m) };

        var row = ValuationCalculator.ForDay(Input(Day, [], prices, tariff) with { PunMonthlyAverageEurMwh = 100m })
            .Single(r => r.Interval.StartUtc == Noon);

        Assert.Equal(Price(0.12m), row.BillPriceEurKwh);
    }

    [Fact]
    public void Vale_l_offerta_iniziata_per_ultima_entro_il_giorno()
    {
        var old = Monoraria(0.10m) with { ValidFrom = new DateOnly(2026, 1, 1) };
        var current = Monoraria(0.20m) with { ValidFrom = new DateOnly(2026, 6, 1) };
        var future = Monoraria(0.50m) with { ValidFrom = new DateOnly(2026, 7, 1) };

        var row = ValuationCalculator.ForDay(Input(Day, [], []) with { Tariffs = [future, old, current] })
            .Single(r => r.Interval.StartUtc == Noon);

        Assert.Equal(Price(0.20m), row.BillPriceEurKwh);
    }

    [Fact]
    public void Con_il_PMG_l_anno_vale_la_serie_piu_favorevole()
    {
        var readings = new List<EnergyReading> { Reading(Noon, 15, export: 1_000_000) }; // 1 MWh
        var input = Input(Day, readings, [ZonalPrice(Noon, 15, 30m)]) with
        {
            Regime = ValuationRegime.RidPmg,
            PriceFloorEurMwh = 47.5m,
        };

        var rows = ValuationCalculator.ForDay(input);

        Assert.Equal(47.5m, ValuationCalculator.ExportValueForYear(ValuationRegime.RidPmg, rows));
        Assert.Equal(30m, ValuationCalculator.ExportValueForYear(ValuationRegime.RidZonale, rows));
    }

    private static decimal Price(decimal energy) => (energy + 0.04m + 0.0227m) * 1.10m;

    private static Tariff Monoraria(decimal price) =>
        new(new DateOnly(2026, 1, 1), OfferKind.Monoraria, price, null, null, null, null, 0m, DataOrigin.Manual);

    private static MarketPrice ZonalPrice(DateTimeOffset start, int minutes, decimal eurMwh) =>
        new(new Interval(start, minutes), MarketPrice.Mgp, Zone, eurMwh);

    private static EnergyReading Reading(
        DateTimeOffset start, int minutes, long? export = null, long? import = null, long? production = null,
        long? consumption = null, long? charge = null, long? discharge = null) =>
        new(new Interval(start, minutes), "test", export, import, production, consumption, charge, discharge);

    private static ValuationInput Input(
        DateOnly day, IReadOnlyList<EnergyReading> readings, IReadOnlyList<MarketPrice> prices, Tariff? tariff = null) =>
        new(day, Zone, ValuationRegime.RidZonale, null, readings, prices,
            tariff is null ? [] : [tariff], [Charges], null);
}

public class ImportCostTests
{
    [Fact]
    public void Il_costo_dei_prelievi_usa_lo_stesso_prezzo_della_bolletta_del_risparmio()
    {
        var noon = new DateTimeOffset(2026, 6, 17, 10, 0, 0, TimeSpan.Zero);
        var tariff = new Tariff(new DateOnly(2026, 1, 1), OfferKind.Monoraria, 0.10m, null, null, null, null, 0m, DataOrigin.Manual);
        var charges = new RegulatedCharges(new DateOnly(2026, 1, 1), 0.04m, 0.0227m, 0.10m);
        var reading = new EnergyReading(new Interval(noon, 15), "test", 0, 500, null, 1500, null, null);

        var row = ValuationCalculator.ForDay(new ValuationInput(new DateOnly(2026, 6, 17), "NORD", ValuationRegime.RidZonale,
            null, [reading], [], [tariff], [charges], null)).Single(r => r.Interval.StartUtc == noon);

        Assert.Equal(500, row.ImportWh);
        Assert.Equal(0.5m * row.BillPriceEurKwh, row.ImportCostEur);
        Assert.Equal(500, ValuationTotals.Of([row]).ImportWh);
    }
}
