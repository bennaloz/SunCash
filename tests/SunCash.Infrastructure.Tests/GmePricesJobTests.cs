using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SunCash.Infrastructure.Gme;
using SunCash.Infrastructure.Http;
using SunCash.Infrastructure.Options;
using SunCash.Infrastructure.Persistence;
using SunCash.Infrastructure.Sync;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace SunCash.Infrastructure.Tests;

public sealed class GmePricesJobTests : IDisposable
{
    private readonly WireMockServer _gme = WireMockServer.Start();

    // Il 24 ottobre 2026 alle 16 italiane: «domani» è il giorno da 100 quarti d'ora della fixture.
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 24, 14, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Importa_zona_configurata_e_PUN_a_15_e_60_minuti_ed_e_idempotente()
    {
        GivenLogin();
        GivenPrices("PT15", GmeFixtures.ZipBase64("gme-zonal-prices-pt15-20261025.json"));
        GivenPrices("PT60", GmeFixtures.ZipBase64("gme-zonal-prices-pt60-20261025.json"));
        await using var db = await TestDatabase.CreateAsync();

        var first = await RunJobAsync(db);
        var second = await RunJobAsync(db);

        Assert.Equal(SyncStatus.Done, first.Status);
        Assert.Equal(SyncStatus.Done, second.Status);
        await using var context = db.NewContext();
        Assert.Equal(200, await context.MarketPrices.CountAsync(p => p.Resolution == 15));
        Assert.Equal(50, await context.MarketPrices.CountAsync(p => p.Resolution == 60));
        Assert.Equal(0, await context.MarketPrices.CountAsync(p => p.Zone == "SUD"));
    }

    [Fact]
    public async Task Manda_token_e_corpo_come_dice_il_manuale()
    {
        GivenLogin();
        GivenPrices("PT15", GmeFixtures.ZipBase64("gme-zonal-prices-pt15-20261025.json"));
        GivenPrices("PT60", GmeFixtures.ZipBase64("gme-zonal-prices-pt60-20261025.json"));
        await using var db = await TestDatabase.CreateAsync();

        await RunJobAsync(db);

        var request = _gme.LogEntries.First(e => (e.RequestMessage?.Path ?? "").EndsWith("RequestData")).RequestMessage!;
        Assert.Equal("Bearer jwt-di-prova", request.Headers!["Authorization"].Single());
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("PublicMarketResults", body.RootElement.GetProperty("Platform").GetString());
        Assert.Equal("ME_ZonalPrices", body.RootElement.GetProperty("DataName").GetString());
        Assert.Equal(20261024, body.RootElement.GetProperty("IntervalStart").GetInt32());
        Assert.Equal(20261025, body.RootElement.GetProperty("IntervalEnd").GetInt32());

        var auth = _gme.LogEntries.First(e => (e.RequestMessage?.Path ?? "").EndsWith("Auth")).RequestMessage!;
        Assert.Contains("\"Login\":\"utente\"", auth.Body);
    }

    [Fact]
    public async Task Se_domani_non_e_ancora_pubblicato_riprova_piu_tardi()
    {
        GivenLogin();
        GivenNoData("PT15");
        GivenNoData("PT60");
        await using var db = await TestDatabase.CreateAsync();

        var outcome = await RunJobAsync(db);

        Assert.Equal(SyncStatus.RetryLater, outcome.Status);
        Assert.Contains("2026-10-25", outcome.Message);
    }

    [Fact]
    public async Task Login_rifiutato_e_un_errore_con_il_motivo()
    {
        _gme.Given(Request.Create().WithPath("/request/api/v1/Auth").UsingPost())
            .RespondWith(Response.Create().WithBodyAsJson(new { Success = false, token = "", Reason = "Password scaduta" }));
        await using var db = await TestDatabase.CreateAsync();

        var ex = await Assert.ThrowsAsync<GmeException>(() => RunJobAsync(db));

        Assert.Contains("Password scaduta", ex.Message);
    }

    [Fact]
    public async Task Con_le_quote_esaurite_tiene_cio_che_e_arrivato_e_riprova_piu_tardi()
    {
        GivenLogin();
        // Un giorno per richiesta: il 24 ottobre arriva, al 25 il GME risponde 429.
        _gme.Given(Request.Create().WithPath("/request/api/v1/RequestData").UsingPost().WithBody(new WildcardMatcher("*20261024*")))
            .RespondWith(Response.Create().WithBodyAsJson(new
            {
                RequestId = "1", FormatType = ".json.zip", ResultRequest = "No data found", ContentResponse = "",
            }));
        _gme.Given(Request.Create().WithPath("/request/api/v1/RequestData").UsingPost().WithBody(new WildcardMatcher("*20261025*")))
            .RespondWith(Response.Create().WithStatusCode(429));
        await using var db = await TestDatabase.CreateAsync();

        var outcome = await RunJobAsync(db, new GmeOptions
        {
            BaseUrl = new Uri(_gme.Url + "/request/"), UserName = "utente", Password = "segreto",
            MinIntervalBetweenRequests = TimeSpan.Zero, MaxDaysPerRequest = 1,
        });

        Assert.Equal(SyncStatus.RetryLater, outcome.Status);
        Assert.Contains("Quote GME esaurite", outcome.Message);
        // Al primo 429 si ferma: nessun altro tentativo nella stessa esecuzione.
        var requests = _gme.LogEntries.Where(e => (e.RequestMessage?.Path ?? "").EndsWith("RequestData")).ToList();
        Assert.Equal(429, requests[^1].ResponseMessage!.StatusCode);
        Assert.Single(requests, e => e.ResponseMessage!.StatusCode is 429);
    }

    [Fact]
    public async Task Fra_due_richieste_aspetta_la_pausa_configurata()
    {
        GivenLogin();
        GivenNoData("PT15");
        GivenNoData("PT60");
        await using var db = await TestDatabase.CreateAsync();

        var run = RunJobAsync(db, new GmeOptions
        {
            BaseUrl = new Uri(_gme.Url + "/request/"), UserName = "utente", Password = "segreto",
            MinIntervalBetweenRequests = TimeSpan.FromSeconds(10),
        });
        await WaitForRequestsAsync(1);
        await Task.Delay(200);

        Assert.Single(_gme.LogEntries);
        Assert.False(run.IsCompleted);
        for (var i = 0; i < 10 && !run.IsCompleted; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(10));
            await Task.WhenAny(run, Task.Delay(200));
        }

        Assert.Equal(SyncStatus.RetryLater, (await run).Status);
    }

    private async Task WaitForRequestsAsync(int count)
    {
        for (var i = 0; i < 100 && _gme.LogEntries.Count() < count; i++)
            await Task.Delay(20);
    }

    private Task<SyncOutcome> RunJobAsync(TestDatabase db) =>
        RunJobAsync(db, new GmeOptions
        {
            BaseUrl = new Uri(_gme.Url + "/request/"), UserName = "utente", Password = "segreto",
            MinIntervalBetweenRequests = TimeSpan.Zero,
        });

    private async Task<SyncOutcome> RunJobAsync(TestDatabase db, GmeOptions gmeOptions)
    {
        await using var context = db.NewContext();
        var options = MsOptions.Create(gmeOptions);
        var job = new GmePricesJob(
            new GmeClient(new HttpClient(), new EndpointThrottle(_clock), options),
            new MarketPriceStore(context, _clock),
            new SyncStateStore(context, _clock),
            options,
            MsOptions.Create(new SyncOptions { StartDate = new DateOnly(2026, 10, 24) }),
            MsOptions.Create(new ValuationOptions { Zone = "NORD" }),
            _clock,
            NullLogger<GmePricesJob>.Instance);
        return await job.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Senza_credenziali_il_job_lo_dice_e_non_chiama_il_GME()
    {
        await using var db = await TestDatabase.CreateAsync();

        var outcome = await RunJobAsync(db, new GmeOptions { BaseUrl = new Uri(_gme.Url + "/request/") });

        Assert.Equal(SyncStatus.Failed, outcome.Status);
        Assert.Contains("Credenziali GME mancanti", outcome.Message);
        Assert.Empty(_gme.LogEntries);
    }

    private void GivenLogin() =>
        _gme.Given(Request.Create().WithPath("/request/api/v1/Auth").UsingPost())
            .RespondWith(Response.Create().WithBodyAsJson(new { Success = true, token = "jwt-di-prova", Reason = (string?)null }));

    private void GivenPrices(string granularity, string base64) =>
        _gme.Given(Request.Create().WithPath("/request/api/v1/RequestData").UsingPost().WithBody(new WildcardMatcher($"*{granularity}*")))
            .RespondWith(Response.Create().WithBodyAsJson(new
            {
                RequestId = "1", FormatType = ".json.zip", ResultRequest = "", ContentResponse = base64,
            }));

    private void GivenNoData(string granularity) =>
        _gme.Given(Request.Create().WithPath("/request/api/v1/RequestData").UsingPost().WithBody(new WildcardMatcher($"*{granularity}*")))
            .RespondWith(Response.Create().WithBodyAsJson(new
            {
                RequestId = "1", FormatType = ".json.zip", ResultRequest = "No data found", ContentResponse = "",
            }));

    public void Dispose() => _gme.Stop();
}
