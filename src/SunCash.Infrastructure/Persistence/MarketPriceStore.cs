using Microsoft.EntityFrameworkCore;
using SunCash.Domain.Prices;
using SunCash.Domain.Time;

namespace SunCash.Infrastructure.Persistence;

/// <summary>Lettura e scrittura dei prezzi GME grezzi.</summary>
public sealed class MarketPriceStore(SunCashDbContext db, TimeProvider clock)
{
    private const string UpsertSql = """
        INSERT INTO market_prices (ts_utc, resolution, market, zone, eur_mwh, imported_at)
        VALUES ($p0, $p1, $p2, $p3, $p4, $p5)
        ON CONFLICT (ts_utc, resolution, market, zone)
        DO UPDATE SET eur_mwh = excluded.eur_mwh, imported_at = excluded.imported_at
        """;

    /// <summary>Inserisce o aggiorna i prezzi.</summary>
    public Task<int> UpsertAsync(IEnumerable<MarketPrice> prices, CancellationToken ct)
    {
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        return SqliteUpsert.RunAsync(db, UpsertSql, prices, p => [
            p.Interval.StartUtc.ToUnixTimeSeconds(), p.Interval.Minutes, p.Market, p.Zone, (double)p.EurMwh, now], ct);
    }

    /// <summary>I prezzi delle zone indicate che iniziano nell'intervallo [from, to).</summary>
    public async Task<IReadOnlyList<MarketPrice>> ListAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, IReadOnlyCollection<string> zones, CancellationToken ct)
    {
        var rows = await db.MarketPrices.AsNoTracking()
            .Where(p => p.TsUtc >= fromUtc && p.TsUtc < toUtc && zones.Contains(p.Zone))
            .ToListAsync(ct);
        return rows.Select(r => new MarketPrice(new Interval(r.TsUtc, r.Resolution), r.Market, r.Zone, r.EurMwh)).ToList();
    }

    /// <summary>Quanti prezzi a 15 minuti della zona esistono per il giorno italiano.</summary>
    public Task<int> CountQuartersAsync(string zone, DateOnly localDate, CancellationToken ct)
    {
        var (from, to) = ItalianTime.DayBoundsUtc(localDate);
        return db.MarketPrices.CountAsync(p => p.Zone == zone && p.Resolution == 15 && p.TsUtc >= from && p.TsUtc < to, ct);
    }
}
