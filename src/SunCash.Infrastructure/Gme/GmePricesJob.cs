using Microsoft.Extensions.Options;
using SunCash.Domain.Prices;
using SunCash.Domain.Time;
using SunCash.Infrastructure.Options;
using SunCash.Infrastructure.Persistence;
using SunCash.Infrastructure.Sync;

namespace SunCash.Infrastructure.Gme;

/// <summary>
/// Job <c>gme-prices</c>: esiti MGP della zona configurata e PUN, a 15 e 60 minuti. Ogni volta
/// rilegge gli ultimi 7 giorni (rettifiche) e il giorno dopo; al primo avvio ricostruisce lo storico
/// da <c>Sync:StartDate</c>. È riuscito quando i prezzi di domani ci sono.
/// </summary>
public sealed class GmePricesJob(
    GmeClient client,
    MarketPriceStore prices,
    SyncStateStore state,
    IOptions<GmeOptions> gme,
    IOptions<SyncOptions> sync,
    IOptions<ValuationOptions> valuation,
    TimeProvider clock) : ISyncJob
{
    /// <summary>Nome del job.</summary>
    public const string JobName = "gme-prices";

    /// <summary>Dal 1° ottobre 2025 l'MGP ha prezzi a 15 minuti; prima solo orari.</summary>
    public static readonly DateOnly FirstQuarterHourDay = new(2025, 10, 1);

    private const int RectificationDays = 7;

    /// <inheritdoc />
    public string Name => JobName;

    /// <inheritdoc />
    public async Task<SyncOutcome> RunAsync(CancellationToken ct)
    {
        var zone = valuation.Value.Zone;
        string[] zones = [zone, MarketPrice.PunZone];
        var today = ItalianTime.LocalDate(clock.GetUtcNow());
        var tomorrow = today.AddDays(1);
        var rollingStart = today.AddDays(-RectificationDays);

        var start = sync.Value.StartDate!.Value;
        var cursor = (await state.GetAsync(JobName, ct))?.Cursor ?? start;
        var from = Max(start, Min(cursor, rollingStart));

        var token = await client.LoginAsync(ct);
        for (var chunkStart = from; chunkStart <= tomorrow; chunkStart = chunkStart.AddDays(gme.Value.MaxDaysPerRequest))
        {
            var chunkEnd = Min(chunkStart.AddDays(gme.Value.MaxDaysPerRequest - 1), tomorrow);

            var hourly = await client.GetZonalPricesAsync(token, chunkStart, chunkEnd, "PT60", ct);
            var imported = GmePriceMapper.ToMarketPrices(hourly, 60, zones).ToList();
            if (chunkEnd >= FirstQuarterHourDay)
            {
                var quarters = await client.GetZonalPricesAsync(token, Max(chunkStart, FirstQuarterHourDay), chunkEnd, "PT15", ct);
                imported.AddRange(GmePriceMapper.ToMarketPrices(quarters, 15, zones));
            }

            await prices.UpsertAsync(imported, ct);

            // Il cursore avanza solo fino all'inizio della finestra mobile: quei giorni si rileggono sempre.
            var newCursor = Min(chunkEnd.AddDays(1), rollingStart);
            if (newCursor > cursor)
                await state.SaveCursorAsync(JobName, cursor = newCursor, null, ct);
        }

        // Si misura sui prezzi a 15 minuti: dal 1° ottobre 2025 il giorno dopo li ha sempre.
        return await prices.CountQuartersAsync(zone, tomorrow, ct) > 0
            ? SyncOutcome.Done()
            : SyncOutcome.RetryLater($"Esiti MGP del {tomorrow:yyyy-MM-dd} non ancora pubblicati.");
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}
