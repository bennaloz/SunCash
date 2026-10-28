using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SunCash.Api.Tests;

/// <summary>
/// L'host parte e risponde attraverso la pipeline HTTP vera, non chiamando il codice a mano.
/// </summary>
public class HealthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_risponde_ok()
    {
        using var client = factory.CreateClient();

        var risposta = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        var corpo = await risposta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("ok", corpo?["status"]);
    }
}
