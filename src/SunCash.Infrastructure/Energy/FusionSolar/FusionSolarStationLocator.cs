using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SunCash.Infrastructure.Energy.FusionSolar;

/// <summary>
/// Il codice dell'impianto (<c>stationCode</c>, «NE=…»): quello configurato, oppure, se manca, quello
/// dell'unico impianto dell'account Northbound, chiesto a FusionSolar una volta e tenuto in
/// <see cref="FusionSolarSession"/>. Con più impianti non sceglie: chiede di configurarlo.
/// </summary>
public sealed class FusionSolarStationLocator(
    FusionSolarClient client, FusionSolarSession session, IOptions<FusionSolarOptions> options, ILogger<FusionSolarStationLocator> logger)
{
    /// <summary>Il codice dell'impianto da usare.</summary>
    public async Task<string> ResolveAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(options.Value.StationCode))
            return options.Value.StationCode;
        if (session.StationCode is { } known)
            return known;

        var stations = await ListAsync(ct);
        if (stations.Count != 1)
            throw new FusionSolarException(stations.Count == 0
                ? "l'account Northbound non vede nessun impianto: va abbinato all'impianto in FusionSolar."
                : $"l'account vede {stations.Count} impianti: imposta Energy:FusionSolar:StationCode con quello giusto (i codici sono nel log).");

        logger.LogInformation("FusionSolar: impianto trovato, codice {StationCode}. Si può fissare in Energy:FusionSolar:StationCode.", stations[0]);
        return session.StationCode = stations[0];
    }

    /// <summary>
    /// I codici degli impianti dell'account. Prima <c>stations</c>, che sostituisce <c>getStationList</c>;
    /// se l'account non è abilitato a quella interfaccia, <c>getStationList</c>.
    /// DA VERIFICARE (docs/analisi.md, «Primo fornitore»): nomi dei campi della risposta; si cercano
    /// sia <c>plantCode</c> sia <c>stationCode</c>, in <c>data.list</c> o in <c>data</c>.
    /// </summary>
    private async Task<IReadOnlyList<string>> ListAsync(CancellationToken ct)
    {
        JsonElement json;
        try
        {
            json = await client.PostAsync("stations", new { pageNo = 1 }, ct);
        }
        catch (FusionSolarException ex) when (ex is not FusionSolarTooFrequentException)
        {
            logger.LogInformation("FusionSolar: «stations» non disponibile ({Message}), provo getStationList.", ex.Message);
            json = await client.PostAsync("getStationList", new { }, ct);
        }

        var codes = new List<string>();
        if (json.TryGetProperty("data", out var data))
        {
            var items = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("list", out var list) ? list : data;
            if (items.ValueKind == JsonValueKind.Array)
                foreach (var item in items.EnumerateArray())
                    if ((Code(item, "plantCode") ?? Code(item, "stationCode")) is { } code)
                        codes.Add(code);
        }

        if (codes.Count > 1)
            logger.LogWarning("FusionSolar: impianti dell'account: {Codes}", string.Join(", ", codes));
        return codes;
    }

    private static string? Code(JsonElement item, string field) =>
        item.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;
}
