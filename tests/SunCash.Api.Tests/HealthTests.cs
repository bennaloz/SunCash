using System.Net;
using System.Net.Http.Json;

namespace SunCash.Api.Tests;

/// <summary>
/// L'host parte e risponde attraverso la pipeline HTTP vera, non chiamando il codice a mano.
/// </summary>
public class HealthTests
{
    [Fact]
    public async Task Health_risponde_ok()
    {
        await using var factory = new SunCashFactory();
        using var client = factory.CreateClient();

        var risposta = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        var corpo = await risposta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("ok", corpo?["status"]);
    }

    [Fact]
    public async Task OpenAPI_descrive_gli_endpoint()
    {
        await using var factory = new SunCashFactory();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/openapi/v1.json");

        Assert.Contains("/api/valuation/day/{date}", json);
        Assert.Contains("/api/tariffs/{validFrom}", json);
    }
}
