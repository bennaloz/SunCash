using Microsoft.Extensions.Logging.Abstractions;
using SunCash.Domain.Energy;
using SunCash.Domain.Time;
using SunCash.Infrastructure.Energy.FusionSolar;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace SunCash.Infrastructure.Tests;

public sealed class FusionSolarTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 6, 17);
    private readonly WireMockServer _cloud = WireMockServer.Start();
    private readonly FusionSolarSession _session = new();

    [Fact]
    public async Task Converte_le_righe_orarie_in_misure_in_Wh()
    {
        GivenLogin("token-1");
        GivenHourly(Response.Create().WithBodyAsJson(HourlyBody()));

        var result = await Source().FetchDayAsync(Day, CancellationToken.None);

        Assert.Equal(EnergyFetchStatus.Done, result.Status);
        var r = result.Readings.Single();
        Assert.Equal(new DateTimeOffset(2026, 6, 17, 10, 0, 0, TimeSpan.Zero), r.Interval.StartUtc);
        Assert.Equal(60, r.Interval.Minutes);
        Assert.Equal("fusionsolar-cloud", r.Source);
        Assert.Equal(1234, r.ExportWh);
        Assert.Equal(500, r.ProductionWh);
        Assert.Null(r.ConsumptionWh); // campo assente nella risposta
    }

    [Fact]
    public async Task Manda_il_token_e_il_giorno_italiano_in_millisecondi()
    {
        GivenLogin("token-1");
        GivenHourly(Response.Create().WithBodyAsJson(HourlyBody()));

        await Source().FetchDayAsync(Day, CancellationToken.None);

        var request = _cloud.LogEntries.Single(e => (e.RequestMessage?.Path ?? "").EndsWith("getKpiStationHour")).RequestMessage!;
        Assert.Equal("token-1", request.Headers!["XSRF-TOKEN"].Single());
        var (dayStart, _) = ItalianTime.DayBoundsUtc(Day);
        Assert.Contains($"\"collectTime\":{dayStart.ToUnixTimeMilliseconds()}", request.Body);
    }

    [Fact]
    public async Task Sessione_scaduta_rifa_il_login_una_volta_e_riprova()
    {
        GivenLogin("token-nuovo");
        _session.Token = "token-vecchio";
        _cloud.Given(Request.Create().WithPath("/thirdData/getKpiStationHour").WithHeader("XSRF-TOKEN", "token-vecchio"))
            .RespondWith(Response.Create().WithBodyAsJson(new { success = false, failCode = 305, data = (object?)null }));
        _cloud.Given(Request.Create().WithPath("/thirdData/getKpiStationHour").WithHeader("XSRF-TOKEN", "token-nuovo"))
            .RespondWith(Response.Create().WithBodyAsJson(HourlyBody()));

        var result = await Source().FetchDayAsync(Day, CancellationToken.None);

        Assert.Equal(EnergyFetchStatus.Done, result.Status);
        Assert.Single(_cloud.LogEntries, e => (e.RequestMessage?.Path ?? "").EndsWith("login"));
        Assert.Equal("token-nuovo", _session.Token);
    }

    [Fact]
    public async Task Frequenza_eccessiva_ferma_fino_al_prossimo_ciclo_senza_riprovare()
    {
        GivenLogin("token-1");
        GivenHourly(Response.Create().WithBodyAsJson(new { success = false, failCode = 407, data = "ACCESS_FREQUENCY_IS_TOO_HIGH" }));

        var result = await Source().FetchDayAsync(Day, CancellationToken.None);

        Assert.Equal(EnergyFetchStatus.RetryNextCycle, result.Status);
        Assert.Single(_cloud.LogEntries, e => (e.RequestMessage?.Path ?? "").EndsWith("getKpiStationHour"));
    }

    [Fact]
    public async Task Success_falso_con_HTTP_200_e_un_errore()
    {
        GivenLogin("token-1");
        GivenHourly(Response.Create().WithBodyAsJson(new { success = false, failCode = 20001, message = "stationCode non valido" }));

        var result = await Source().FetchDayAsync(Day, CancellationToken.None);

        Assert.Equal(EnergyFetchStatus.Error, result.Status);
        Assert.Contains("20001", result.Message);
    }

    private FusionSolarEnergySource Source(string stationCode = "NE=00000000") // segnaposto, non un impianto reale
    {
        var options = MsOptions.Create(new FusionSolarOptions
        {
            BaseUrl = new Uri(_cloud.Url + "/thirdData/"),
            UserName = "utente",
            SystemCode = "segreto",
            StationCode = stationCode,
            MinIntervalPerEndpoint = TimeSpan.Zero,
        });
        var client = new FusionSolarClient(new HttpClient(), _session, new EndpointThrottle(TimeProvider.System), options);
        var locator = new FusionSolarStationLocator(client, _session, options, NullLogger<FusionSolarStationLocator>.Instance);
        return new FusionSolarEnergySource(client, locator, options, NullLogger<FusionSolarEnergySource>.Instance);
    }

    [Fact]
    public async Task Senza_codice_configurato_usa_l_unico_impianto_dell_account_e_lo_ricorda()
    {
        GivenLogin("token-1");
        _cloud.Given(Request.Create().WithPath("/thirdData/stations").UsingPost())
            .RespondWith(Response.Create().WithBodyAsJson(new
            {
                success = true, failCode = 0,
                data = new { list = new[] { new { plantCode = "NE=11111111", plantName = "Impianto di prova" } } },
            }));
        GivenHourly(Response.Create().WithBodyAsJson(HourlyBody()));

        var source = Source(stationCode: "");
        await source.FetchDayAsync(Day, CancellationToken.None);
        var result = await source.FetchDayAsync(Day, CancellationToken.None);

        Assert.Equal(EnergyFetchStatus.Done, result.Status);
        Assert.Single(_cloud.LogEntries, e => (e.RequestMessage?.Path ?? "").EndsWith("stations"));
        Assert.All(_cloud.LogEntries.Where(e => (e.RequestMessage?.Path ?? "").EndsWith("getKpiStationHour")),
            e => Assert.Contains("NE=11111111", e.RequestMessage!.Body));
    }

    [Fact]
    public async Task Con_piu_impianti_non_sceglie_e_chiede_di_configurarlo()
    {
        GivenLogin("token-1");
        _cloud.Given(Request.Create().WithPath("/thirdData/stations").UsingPost())
            .RespondWith(Response.Create().WithBodyAsJson(new
            {
                success = true, failCode = 0,
                data = new { list = new[] { new { plantCode = "NE=1" }, new { plantCode = "NE=2" } } },
            }));

        var result = await Source(stationCode: "").FetchDayAsync(Day, CancellationToken.None);

        Assert.Equal(EnergyFetchStatus.Error, result.Status);
        Assert.Contains("2 impianti", result.Message);
        Assert.DoesNotContain(_cloud.LogEntries, e => (e.RequestMessage?.Path ?? "").EndsWith("getKpiStationHour"));
    }

    [Fact]
    public async Task Se_stations_non_e_abilitata_prova_getStationList()
    {
        GivenLogin("token-1");
        _cloud.Given(Request.Create().WithPath("/thirdData/stations").UsingPost())
            .RespondWith(Response.Create().WithBodyAsJson(new { success = false, failCode = 401, message = "no access" }));
        _cloud.Given(Request.Create().WithPath("/thirdData/getStationList").UsingPost())
            .RespondWith(Response.Create().WithBodyAsJson(new
            {
                success = true, failCode = 0, data = new[] { new { stationCode = "NE=22222222" } },
            }));
        GivenHourly(Response.Create().WithBodyAsJson(HourlyBody()));

        var result = await Source(stationCode: "").FetchDayAsync(Day, CancellationToken.None);

        Assert.Equal(EnergyFetchStatus.Done, result.Status);
        Assert.Contains("NE=22222222", _cloud.LogEntries.Single(e => (e.RequestMessage?.Path ?? "").EndsWith("getKpiStationHour")).RequestMessage!.Body);
    }

    [Fact]
    public async Task Con_il_codice_configurato_non_cerca_l_impianto()
    {
        GivenLogin("token-1");
        GivenHourly(Response.Create().WithBodyAsJson(HourlyBody()));

        await Source().FetchDayAsync(Day, CancellationToken.None);

        Assert.DoesNotContain(_cloud.LogEntries, e => (e.RequestMessage?.Path ?? "").EndsWith("stations"));
    }

    private static object HourlyBody() => new
    {
        success = true,
        failCode = 0,
        data = new[]
        {
            new
            {
                collectTime = new DateTimeOffset(2026, 6, 17, 10, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                stationCode = "NE=00000000",
                dataItemMap = new Dictionary<string, object?>
                {
                    ["ongrid_power"] = 1.234, ["inverter_power"] = "0.5", ["buyPower"] = null,
                },
            },
        },
    };

    private void GivenLogin(string token) =>
        _cloud.Given(Request.Create().WithPath("/thirdData/login").UsingPost())
            .RespondWith(Response.Create().WithHeader("xsrf-token", token).WithBodyAsJson(new { success = true, failCode = 0 }));

    private void GivenHourly(IResponseBuilder response) =>
        _cloud.Given(Request.Create().WithPath("/thirdData/getKpiStationHour").UsingPost()).RespondWith(response);

    public void Dispose() => _cloud.Stop();
}

public class EndpointThrottleTests
{
    [Fact]
    public async Task La_seconda_chiamata_aspetta_l_intervallo_minimo()
    {
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var throttle = new EndpointThrottle(clock);

        await throttle.WaitTurnAsync("getKpiStationHour", TimeSpan.FromMinutes(5), CancellationToken.None);
        var second = throttle.WaitTurnAsync("getKpiStationHour", TimeSpan.FromMinutes(5), CancellationToken.None);
        var otherEndpoint = throttle.WaitTurnAsync("login", TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.True(otherEndpoint.IsCompleted);
        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.False(second.IsCompleted);
        clock.Advance(TimeSpan.FromMinutes(1));
        await second;
    }
}
