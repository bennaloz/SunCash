using System.Globalization;
using System.IO.Compression;
using System.Text.Json;

namespace SunCash.Infrastructure.Gme;

/// <summary>
/// Legge la risposta di <c>RequestData</c>: lo zip in Base64 contiene un file <c>.json</c> con
/// l'elenco dei prezzi. Tollera numeri scritti come stringhe e nomi di campo in qualunque maiuscola.
/// </summary>
public static class GmeZonalPriceParser
{
    /// <summary>Decodifica il contenuto Base64 e ne interpreta il JSON.</summary>
    public static IReadOnlyList<GmeZonalPriceRecord> ParseContentResponse(string base64Zip)
    {
        using var zip = new ZipArchive(new MemoryStream(Convert.FromBase64String(base64Zip)), ZipArchiveMode.Read);
        var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    ?? throw new GmeException("Lo zip del GME non contiene un file .json.");
        using var stream = entry.Open();
        return ParseJson(stream);
    }

    /// <summary>Interpreta il JSON dei prezzi zonali.</summary>
    public static IReadOnlyList<GmeZonalPriceRecord> ParseJson(Stream json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new GmeException("Il JSON del GME non è un elenco.");

        return document.RootElement.EnumerateArray().Select(e => new GmeZonalPriceRecord(
            Int(e, "FlowDate") ?? throw new GmeException("Prezzo senza FlowDate."),
            Int(e, "Hour") ?? throw new GmeException("Prezzo senza Hour."),
            Int(e, "Period"),
            Str(e, "Market") ?? "",
            Str(e, "Zone") ?? "",
            Dec(e, "Price"),
            Str(e, "Notes"))).ToList();
    }

    private static JsonElement? Field(JsonElement e, string name)
    {
        foreach (var p in e.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : p.Value;
        return null;
    }

    private static string? Str(JsonElement e, string name) =>
        Field(e, name) is { } v ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText()) : null;

    private static int? Int(JsonElement e, string name) =>
        Str(e, name) is { Length: > 0 } s ? int.Parse(s, CultureInfo.InvariantCulture) : null;

    private static decimal? Dec(JsonElement e, string name) =>
        Str(e, name) is { Length: > 0 } s
            ? decimal.Parse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture)
            : null;
}

/// <summary>Errore nel dialogo con il GME.</summary>
public sealed class GmeException(string message) : Exception(message);
