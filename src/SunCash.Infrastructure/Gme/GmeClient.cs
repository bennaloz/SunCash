using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SunCash.Infrastructure.Http;

namespace SunCash.Infrastructure.Gme;

/// <summary>
/// Client dell'API del GME (manuale tecnico del 15/10/2025): <c>Auth</c> per il JWT,
/// <c>RequestData</c> per gli esiti MGP, <c>GetMyQuotas</c> per le quote. Fra due richieste aspetta
/// <see cref="GmeOptions.MinIntervalBetweenRequests"/>; se il GME risponde 429 le quote sono finite e
/// lancia <see cref="GmeQuotaExceededException"/>, senza riprovare.
/// </summary>
public sealed class GmeClient(HttpClient http, EndpointThrottle throttle, IOptions<GmeOptions> options)
{
    private const string ThrottleKey = "gme:requests";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    // I nomi dei campi vanno mandati come nel manuale (Login, Platform, …): niente camelCase.
    private static readonly JsonSerializerOptions Outgoing = new() { PropertyNamingPolicy = null };

    /// <summary>Login: restituisce il token JWT.</summary>
    public async Task<string> LoginAsync(CancellationToken ct)
    {
        var o = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(o.BaseUrl, "api/v1/Auth"))
        {
            Content = JsonContent.Create(new { Login = o.UserName, Password = o.Password }, options: Outgoing),
        };
        using var response = await SendAsync(request, ct);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(Json, ct);
        if (body is not { Success: true, Token: { Length: > 0 } token })
            throw new GmeException($"Login GME rifiutato: {body?.Reason ?? "risposta vuota"}.");
        return token;
    }

    /// <summary>Prezzi zonali MGP (con il PUN) dei giorni [from, to], alla granularità <c>PT15</c> o <c>PT60</c>.</summary>
    public async Task<IReadOnlyList<GmeZonalPriceRecord>> GetZonalPricesAsync(
        string token, DateOnly from, DateOnly to, string granularity, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(options.Value.BaseUrl, "api/v1/RequestData"))
        {
            Content = JsonContent.Create(new
            {
                Platform = "PublicMarketResults",
                Segment = "MGP",
                DataName = "ME_ZonalPrices",
                IntervalStart = int.Parse(from.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
                IntervalEnd = int.Parse(to.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
                Attributes = new Dictionary<string, string> { ["GranularityType"] = granularity },
            }, options: Outgoing),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await SendAsync(request, ct);
        var body = await response.Content.ReadFromJsonAsync<DataResponse>(Json, ct);
        if (body is null)
            throw new GmeException("Risposta GME vuota.");

        // Nessun esito (es. il giorno dopo non è ancora pubblicato) non è un errore: è un elenco vuoto.
        if (string.IsNullOrEmpty(body.ContentResponse))
            return string.IsNullOrEmpty(body.ResultRequest) || IsNoData(body.ResultRequest)
                ? []
                : throw new GmeException($"Richiesta GME fallita: {body.ResultRequest}");

        return GmeZonalPriceParser.ParseContentResponse(body.ContentResponse);
    }

    /// <summary>Quote dell'utente così come le restituisce il GME (JSON grezzo, da scrivere nel log).</summary>
    public async Task<string> GetMyQuotasAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(options.Value.BaseUrl, "api/v1/GetMyQuotas"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await SendAsync(request, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        await throttle.WaitTurnAsync(ThrottleKey, options.Value.MinIntervalBetweenRequests, ct);
        var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            response.Dispose();
            throw new GmeQuotaExceededException();
        }

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            response.Dispose();
            throw;
        }

        return response;
    }

    // DA VERIFICARE (docs/analisi.md, «Prezzi: API del GME»): il testo esatto di ResultRequest quando
    // non ci sono esiti non è documentato; si riconosce in modo largo e, se diverso, arriva come errore.
    private static bool IsNoData(string result) =>
        result.Contains("no data", StringComparison.OrdinalIgnoreCase) ||
        result.Contains("nessun", StringComparison.OrdinalIgnoreCase);

    private sealed record AuthResponse(bool Success, string? Token, string? Reason);

    private sealed record DataResponse(string? RequestId, string? FormatType, string? ResultRequest, string? ContentResponse);
}
