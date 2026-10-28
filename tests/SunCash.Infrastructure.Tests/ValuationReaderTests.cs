using Microsoft.Extensions.Time.Testing;
using SunCash.Domain.Energy;
using SunCash.Domain.Prices;
using SunCash.Domain.Tariffs;
using SunCash.Domain.Time;
using SunCash.Domain.Valuation;
using SunCash.Infrastructure.Options;
using SunCash.Infrastructure.Persistence;
using SunCash.Infrastructure.Valuation;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace SunCash.Infrastructure.Tests;

/// <summary>
/// Criterio della milestone 5: i totali letti dal database coincidono con il calcolatore applicato
/// in memoria agli stessi dati. Prova che persistenza e lettura non perdono né spostano niente.
/// </summary>
public class ValuationReaderTests
{
    private const string Zone = "NORD";
    private static readonly DateOnly[] Days = [new(2026, 5, 31), new(2026, 6, 1), new(2026, 6, 2)];

    private static readonly Tariff[] Tariffs =
    [
        new(new DateOnly(2026, 5, 1), OfferKind.Monoraria, 0.11m, null, null, null, null, 0m, DataOrigin.Manual),
        new(new DateOnly(2026, 6, 1), OfferKind.Fasce, 0.13m, 0.12m, 0.09m, null, null, 0m, DataOrigin.Manual),
        new(new DateOnly(2026, 6, 2), OfferKind.IndicizzataPun, null, null, null, 0.015m, PunBasis.Mensile, 0.102m, DataOrigin.Manual),
    ];

    private static readonly RegulatedCharges Charges = new(new DateOnly(2026, 1, 1), 0.0412m, 0.0227m, 0.10m);

    [Fact]
    public async Task I_totali_letti_dal_database_coincidono_con_il_calcolatore_in_memoria()
    {
        var (readings, prices) = Dataset();
        await using var db = await TestDatabase.CreateAsync();
        var clock = new FakeTimeProvider();
        await using (var context = db.NewContext())
        {
            await new EnergyReadingStore(context, clock).UpsertAsync(readings, CancellationToken.None);
            await new MarketPriceStore(context, clock).UpsertAsync(prices, CancellationToken.None);
            var manual = new ManualDataStore(context);
            foreach (var t in Tariffs)
                await manual.SaveTariffAsync(t, CancellationToken.None);
            await manual.UpsertRegulatedChargesAsync([Charges], CancellationToken.None);
        }

        IReadOnlyList<ValuedDay> fromDb;
        await using (var context = db.NewContext())
        {
            var reader = new ValuationReader(new EnergyReadingStore(context, clock), new MarketPriceStore(context, clock),
                new ManualDataStore(context), new NamedSource("test"),
                MsOptions.Create(new ValuationOptions { Zone = Zone }));
            fromDb = await reader.ForDaysAsync(Days[0], Days[^1], CancellationToken.None);
        }

        Assert.Equal(Days.Length, fromDb.Count);
        foreach (var valued in fromDb)
        {
            var expected = ValuationTotals.Of(InMemory(valued.Day, readings, prices));
            var actual = valued.Totals;

            Assert.Equal(expected.ExportWh, actual.ExportWh);
            Assert.Equal(expected.SelfConsumptionWh, actual.SelfConsumptionWh);
            Assert.Equal(expected.ExportValueEur, actual.ExportValueEur, 9);
            Assert.Equal(expected.SavingsEur, actual.SavingsEur, 9);
            Assert.True(actual.SavingsEur > 0, $"{valued.Day}: risparmio non calcolato");
            Assert.Equal(0, actual.RowsWithoutSavings);
        }
    }

    private static IReadOnlyList<ValuationRow> InMemory(DateOnly day, List<EnergyReading> readings, List<MarketPrice> prices)
    {
        var (start, end) = ItalianTime.DayBoundsUtc(day);
        var month = prices.Where(p => ItalianTime.LocalDate(p.Interval.StartUtc) is var d && d.Year == day.Year && d.Month == day.Month);
        return ValuationCalculator.ForDay(new ValuationInput(day, Zone, ValuationRegime.RidZonale, null,
            readings.Where(r => r.Interval.StartUtc >= start && r.Interval.StartUtc < end).ToList(),
            prices.Where(p => p.Interval.StartUtc >= start && p.Interval.StartUtc < end).ToList(),
            Tariffs, [Charges], PunMonthlyAverage.Of(month)));
    }

    /// <summary>Ore pari solo col dato orario, ore dispari a 15 minuti; prezzi zonali a 15 e 60, PUN orario.</summary>
    private static (List<EnergyReading>, List<MarketPrice>) Dataset()
    {
        var readings = new List<EnergyReading>();
        var prices = new List<MarketPrice>();
        foreach (var day in Days)
        {
            var quarters = ItalianTime.QuarterStartsUtc(day);
            for (var q = 0; q < quarters.Count; q++)
            {
                var t = quarters[q];
                prices.Add(new MarketPrice(new Interval(t, 15), MarketPrice.Mgp, Zone, 80m + q * 0.37m));
                if (q % 4 != 0)
                    continue;

                var hour = q / 4;
                prices.Add(new MarketPrice(new Interval(t, 60), MarketPrice.Mgp, Zone, 90m + hour * 1.13m));
                prices.Add(new MarketPrice(new Interval(t, 60), MarketPrice.Mgp, MarketPrice.PunZone, 100m + hour * 0.71m));
                if (hour % 2 == 0)
                    readings.Add(new EnergyReading(new Interval(t, 60), "test", 400 + hour * 13, 150, 900 + hour * 7, 650, 100, 20));
                else
                    for (var k = 0; k < 4; k++)
                        readings.Add(new EnergyReading(new Interval(t.AddMinutes(15 * k), 15), "test", 100 + k, 40, 220, 160 + k, 25, 5));
            }
        }

        return (readings, prices);
    }

    private sealed class NamedSource(string id) : IEnergySource
    {
        public string Id => id;
        public EnergySourceCapabilities Capabilities { get; } = new([15, 60], true);
        public Task<EnergyFetchResult> FetchDayAsync(DateOnly localDate, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
