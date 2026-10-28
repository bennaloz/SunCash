using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SunCash.Infrastructure.Energy.FusionSolar;

/// <summary>
/// Client Northbound di FusionSolar. Una sola sessione: il token sta in <see cref="FusionSolarSession"/>
/// e si rifà il login solo quando la risposta dice sessione scaduta. <c>success</c> e <c>failCode</c>
/// si controllano sempre, anche con HTTP 200.
/// </summary>
public sealed class FusionSolarClient(
    HttpClient http, FusionSolarSession session, EndpointThrottle throttle, IOptions<FusionSolarOptions> options)
{
    // Fonte: integrazione open source HomeAssistant-FusionSolar.
    // DA VERIFICARE (docs/analisi.md): tabella failCode della Northbound Interface Reference.
    internal const int SessionExpired = 305;
    internal const int TooFrequent = 407;

    /// <summary>Chiama un endpoint; rifà il login una volta se la sessione è scaduta.</summary>
    public async Task<JsonElement> PostAsync(string endpoint, object body, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var token = session.Token ?? await LoginAsync(ct);
            var json = await SendAsync(endpoint, body, token, ct);
            var failCode = json.TryGetProperty("failCode", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetInt32() : 0;

            if (failCode == SessionExpired && attempt == 0)
            {
                session.Token = null;
                continue;
            }

            if (failCode == TooFrequent)
                throw new FusionSolarTooFrequentException(Message(json));

            var success = json.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
            if (!success || failCode != 0)
                throw new FusionSolarException($"{endpoint}: failCode {failCode}, {Message(json)}");

            return json;
        }
    }

    private async Task<string> LoginAsync(CancellationToken ct)
    {
        var o = options.Value;
        await throttle.WaitTurnAsync("login", o.MinIntervalPerEndpoint, ct);
        using var response = await http.PostAsJsonAsync(new Uri(o.BaseUrl!, "login"),
            new { userName = o.UserName, systemCode = o.SystemCode }, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (json.TryGetProperty("failCode", out var f) && f.ValueKind == JsonValueKind.Number && f.GetInt32() == TooFrequent)
            throw new FusionSolarTooFrequentException(Message(json));
        if (!response.Headers.TryGetValues("xsrf-token", out var values) || values.FirstOrDefault() is not { Length: > 0 } token)
            throw new FusionSolarException($"Login FusionSolar senza token: {Message(json)}");

        return session.Token = token;
    }

    private async Task<JsonElement> SendAsync(string endpoint, object body, string token, CancellationToken ct)
    {
        await throttle.WaitTurnAsync(endpoint, options.Value.MinIntervalPerEndpoint, ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(options.Value.BaseUrl!, endpoint))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("XSRF-TOKEN", token);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    private static string Message(JsonElement json) =>
        json.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() ?? "" :
        json.TryGetProperty("data", out var d) ? d.ToString() : "";
}

/// <summary>Stato della sessione FusionSolar in memoria (singleton: una sola sessione attiva).</summary>
public sealed class FusionSolarSession
{
    /// <summary>Token corrente, <c>null</c> prima del login o dopo una sessione scaduta.</summary>
    public string? Token { get; set; }

    /// <summary>Codice dell'impianto trovato da <see cref="FusionSolarStationLocator"/>, se non configurato.</summary>
    public string? StationCode { get; set; }
}

/// <summary>Errore restituito da FusionSolar.</summary>
public class FusionSolarException(string message) : Exception(message);

/// <summary>FusionSolar chiede di rallentare (failCode 407): ci si ferma fino al prossimo ciclo.</summary>
public sealed class FusionSolarTooFrequentException(string message) : FusionSolarException(message);
