using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SunCash.Domain.Energy;
using SunCash.Domain.Time;

namespace SunCash.Infrastructure.Energy.FusionSolar;

/// <summary>
/// Fornitore FusionSolar (cloud Northbound). Prima versione: dati orari di <c>getKpiStationHour</c>;
/// il dettaglio a 5 minuti del meter (<c>getDevHistoryKpi</c>) è un'estensione successiva.
/// </summary>
public sealed class FusionSolarEnergySource(
    FusionSolarClient client, IOptions<FusionSolarOptions> options, ILogger<FusionSolarEnergySource> logger) : IEnergySource
{
    /// <summary>Id salvato in <c>energy_readings.source</c>.</summary>
    public const string SourceId = "fusionsolar-cloud";

    /// <summary>Nome con cui si sceglie in <c>Energy:Provider</c>.</summary>
    public const string ProviderName = "FusionSolar";

    private static int _keysLogged;

    /// <inheritdoc />
    public string Id => SourceId;

    /// <inheritdoc />
    public EnergySourceCapabilities Capabilities { get; } = new([60], CanReadHistory: true);

    /// <inheritdoc />
    public async Task<EnergyFetchResult> FetchDayAsync(DateOnly localDate, CancellationToken ct)
    {
        var o = options.Value;
        var (dayStart, dayEnd) = ItalianTime.DayBoundsUtc(localDate);
        try
        {
            var json = await client.PostAsync("getKpiStationHour",
                new { stationCodes = o.StationCode, collectTime = dayStart.ToUnixTimeMilliseconds() }, ct);

            var readings = new List<EnergyReading>();
            if (json.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    var start = DateTimeOffset.FromUnixTimeMilliseconds(item.GetProperty("collectTime").GetInt64());
                    if (start < dayStart || start >= dayEnd || !item.TryGetProperty("dataItemMap", out var map))
                        continue;

                    LogKeysOnce(map);
                    var f = o.Fields;
                    readings.Add(new EnergyReading(new Interval(start, 60), SourceId,
                        Wh(map, f.Export), Wh(map, f.Import), Wh(map, f.Production),
                        Wh(map, f.Consumption), Wh(map, f.BatteryCharge), Wh(map, f.BatteryDischarge)));
                }
            }

            return EnergyFetchResult.Done(readings);
        }
        catch (FusionSolarTooFrequentException ex)
        {
            return EnergyFetchResult.RetryNextCycle($"FusionSolar: frequenza eccessiva ({ex.Message}).");
        }
        catch (Exception ex) when (ex is FusionSolarException or HttpRequestException or JsonException or KeyNotFoundException)
        {
            return EnergyFetchResult.Error($"FusionSolar: {ex.Message}");
        }
    }

    /// <summary>kWh del campo convertiti in Wh; <c>null</c> se il campo non c'è o è vuoto.</summary>
    private static long? Wh(JsonElement map, string field)
    {
        if (string.IsNullOrEmpty(field) || !map.TryGetProperty(field, out var v))
            return null;
        decimal? kwh = v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(v.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };
        return kwh is { } k ? (long)Math.Round(k * 1000m, MidpointRounding.AwayFromZero) : null;
    }

    /// <summary>Le chiavi di <c>dataItemMap</c> si scrivono nel log una volta per processo, per
    /// verificare i nomi dei campi sulla risposta reale.</summary>
    private void LogKeysOnce(JsonElement map)
    {
        if (Interlocked.Exchange(ref _keysLogged, 1) == 0)
            logger.LogInformation("FusionSolar getKpiStationHour, chiavi di dataItemMap: {Keys}",
                string.Join(", ", map.EnumerateObject().Select(p => p.Name)));
    }
}
