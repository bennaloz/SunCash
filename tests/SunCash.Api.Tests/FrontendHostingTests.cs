using System.Net;

namespace SunCash.Api.Tests;

/// <summary>Su App Service l'API serve anche il frontend Angular pubblicato in wwwroot.</summary>
public sealed class FrontendHostingTests : IDisposable
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"suncash-wwwroot-{Guid.NewGuid():N}");

    public FrontendHostingTests()
    {
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<app-root>SunCash</app-root>");
        File.WriteAllText(Path.Combine(_webRoot, "main.js"), "console.log('ok')");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/calendario")]
    [InlineData("/giorno/2026-10-25")]
    public async Task Le_rotte_del_frontend_tornano_a_index_html(string path)
    {
        await using var factory = new SunCashFactory { WebRoot = _webRoot };
        using var client = factory.CreateClient();

        var body = await client.GetStringAsync(path);

        Assert.Contains("<app-root>", body);
    }

    [Fact]
    public async Task I_file_statici_si_servono_come_sono()
    {
        await using var factory = new SunCashFactory { WebRoot = _webRoot };
        using var client = factory.CreateClient();

        Assert.Equal("console.log('ok')", await client.GetStringAsync("/main.js"));
    }

    [Fact]
    public async Task Un_indirizzo_api_sconosciuto_e_404_e_non_la_pagina_del_frontend()
    {
        await using var factory = new SunCashFactory { WebRoot = _webRoot };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/non-esiste");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);
}
