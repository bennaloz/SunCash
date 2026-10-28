namespace SunCash.Infrastructure.Gme;

/// <summary>Un elemento di <c>ME_ZonalPrices</c> così come lo pubblica il GME (manuale tecnico § 5.52).</summary>
/// <param name="FlowDate">Giorno di flusso <c>yyyyMMdd</c>.</param>
/// <param name="Hour">Ora di flusso 1-25.</param>
/// <param name="Period">Quarto d'ora del giorno 1-100, per i dati a 15 minuti.</param>
/// <param name="Price">€/MWh; <c>null</c> se l'asta è stata cancellata.</param>
public sealed record GmeZonalPriceRecord(int FlowDate, int Hour, int? Period, string Market, string Zone, decimal? Price, string? Notes);
