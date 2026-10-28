using System.Text.Json;
using SunCash.Domain.Tariffs;

namespace SunCash.Infrastructure.Persistence;

/// <summary>
/// Carica all'avvio la parte regolata ARERA dal file versionato nel repo
/// (<c>Persistence/Seed/regulated-charges.json</c>): è un dato pubblico, l'utente non lo inserisce.
/// </summary>
public static class RegulatedChargesSeed
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Le righe del file incorporato nell'assembly.</summary>
    public static IReadOnlyList<RegulatedCharges> Load()
    {
        using var stream = typeof(RegulatedChargesSeed).Assembly
            .GetManifestResourceStream("SunCash.Infrastructure.Persistence.Seed.regulated-charges.json")
            ?? throw new InvalidOperationException("File della parte regolata non incorporato.");
        var file = JsonSerializer.Deserialize<SeedFile>(stream, Json) ?? new SeedFile([]);
        return file.Charges.Select(c => new RegulatedCharges(c.ValidFrom, c.NetworkEurKwh, c.ExciseEurKwh, c.VatRate)).ToList();
    }

    /// <summary>Scrive le righe del file nel database.</summary>
    public static Task ApplyAsync(ManualDataStore store, CancellationToken ct) => store.UpsertRegulatedChargesAsync(Load(), ct);

    private sealed record SeedFile(List<SeedRow> Charges);

    private sealed record SeedRow(DateOnly ValidFrom, decimal NetworkEurKwh, decimal ExciseEurKwh, decimal VatRate);
}
