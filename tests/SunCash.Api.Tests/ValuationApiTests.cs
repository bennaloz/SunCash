using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SunCash.Domain.Energy;
using SunCash.Domain.Prices;
using SunCash.Domain.Tariffs;
using SunCash.Domain.Time;
using SunCash.Infrastructure.Persistence;

namespace SunCash.Api.Tests;

/// <summary>Criterio della milestone 6: giorno normale, giorno del cambio d'ora, cambio di tariffa a metà mese.</summary>
public class ValuationApiTests
{
    [Theory]
    [InlineData("2026-06-17", 96)]
    [InlineData("2026-10-25", 100)]
    [InlineData("2026-03-29", 92)]
    public async Task Il_giorno_ha_una_riga_per_quarto_d_ora_con_etichetta_italiana(string date, int quarters)
    {
        await using var factory = new SunCashFactory();
        await SeedQuarterDataAsync(factory, DateOnly.Parse(date));
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/valuation/day/{date}"));

        var rows = json.RootElement.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(quarters, rows.Count);
        Assert.Equal("00:00", rows[0].GetProperty("localLabel").GetString());
        Assert.Equal("23:45", rows[^1].GetProperty("localLabel").GetString());
        Assert.Equal("Quarter", rows[0].GetProperty("resolution").GetString());
        var exportKwh = json.RootElement.GetProperty("totals").GetProperty("exportKwh").GetDecimal();
        Assert.Equal(quarters * 0.1m, exportKwh);
    }

    [Fact]
    public async Task Cambio_di_offerta_a_meta_mese_cambia_il_prezzo_della_bolletta_dal_giorno_giusto()
    {
        await using var factory = new SunCashFactory();
        using var client = factory.CreateClient();
        await SeedRegulatedChargesAsync(factory);

        await PutTariffAsync(client, "2026-06-01", 0.10m);
        await PutTariffAsync(client, "2026-06-15", 0.20m);

        var before = await BillPriceAtNoonAsync(client, "2026-06-14");
        var after = await BillPriceAtNoonAsync(client, "2026-06-15");
        Assert.Equal((0.10m + 0.04m + 0.0227m) * 1.10m, before);
        Assert.Equal((0.20m + 0.04m + 0.0227m) * 1.10m, after);
    }

    [Fact]
    public async Task Un_offerta_incompleta_e_rifiutata_con_il_campo_che_manca()
    {
        await using var factory = new SunCashFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/tariffs/2026-06-01",
            new { kind = "Fasce", energyF1EurKwh = 0.1, lossesFactor = 0, origin = "Manual" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("EnergyF2EurKwh", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_periodo_rovesciato_e_rifiutato()
    {
        await using var factory = new SunCashFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/valuation/days?from=2026-06-10&to=2026-06-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Lo_stato_segnala_la_parte_regolata_mancante_e_i_job_mai_partiti()
    {
        await using var factory = new SunCashFactory();
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/sync/status"));

        Assert.True(json.RootElement.GetProperty("regulatedChargesMissing").GetBoolean());
        Assert.Equal("fusionsolar-cloud", json.RootElement.GetProperty("activeSource").GetString());
        Assert.All(json.RootElement.GetProperty("jobs").EnumerateArray(), j => Assert.True(j.GetProperty("stale").GetBoolean()));
    }

    private static async Task PutTariffAsync(HttpClient client, string validFrom, decimal price)
    {
        var response = await client.PutAsJsonAsync($"/api/tariffs/{validFrom}",
            new { kind = "Monoraria", energyF1EurKwh = price, lossesFactor = 0, origin = "Manual" });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<decimal> BillPriceAtNoonAsync(HttpClient client, string date)
    {
        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/valuation/day/{date}"));
        return json.RootElement.GetProperty("rows").EnumerateArray()
            .Single(r => r.GetProperty("localLabel").GetString() == "12:00")
            .GetProperty("billPriceEurKwh").GetDecimal();
    }

    private static async Task SeedRegulatedChargesAsync(SunCashFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ManualDataStore>()
            .UpsertRegulatedChargesAsync([new RegulatedCharges(new DateOnly(2026, 1, 1), 0.04m, 0.0227m, 0.10m)], CancellationToken.None);
    }

    /// <summary>100 Wh immessi in ogni quarto d'ora del giorno, con prezzo a 15 minuti.</summary>
    private static async Task SeedQuarterDataAsync(SunCashFactory factory, DateOnly day)
    {
        using var scope = factory.Services.CreateScope();
        var quarters = ItalianTime.QuarterStartsUtc(day);
        await scope.ServiceProvider.GetRequiredService<EnergyReadingStore>().UpsertAsync(
            quarters.Select(q => new EnergyReading(new Interval(q, 15), "fusionsolar-cloud", 100, 0, 100, 0, 0, 0)), CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<MarketPriceStore>().UpsertAsync(
            quarters.Select(q => new MarketPrice(new Interval(q, 15), MarketPrice.Mgp, "NORD", 100m)), CancellationToken.None);
    }
}
