using Microsoft.EntityFrameworkCore;
using SunCash.Domain.Energy;
using SunCash.Domain.Time;

namespace SunCash.Infrastructure.Persistence;

/// <summary>Lettura e scrittura delle misure di energia grezze.</summary>
public sealed class EnergyReadingStore(SunCashDbContext db, TimeProvider clock)
{
    private const string UpsertSql = """
        INSERT INTO energy_readings (ts_utc, resolution, source, export_wh, import_wh, production_wh,
                                     consumption_wh, battery_charge_wh, battery_discharge_wh, self_consumption_wh,
                                     imported_at)
        VALUES ($p0, $p1, $p2, $p3, $p4, $p5, $p6, $p7, $p8, $p9, $p10)
        ON CONFLICT (ts_utc, resolution, source) DO UPDATE SET
            export_wh = excluded.export_wh, import_wh = excluded.import_wh,
            production_wh = excluded.production_wh, consumption_wh = excluded.consumption_wh,
            battery_charge_wh = excluded.battery_charge_wh, battery_discharge_wh = excluded.battery_discharge_wh,
            self_consumption_wh = excluded.self_consumption_wh,
            imported_at = excluded.imported_at
        """;

    /// <summary>Inserisce o aggiorna le misure.</summary>
    public Task<int> UpsertAsync(IEnumerable<EnergyReading> readings, CancellationToken ct)
    {
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        return SqliteUpsert.RunAsync(db, UpsertSql, readings, r => [
            r.Interval.StartUtc.ToUnixTimeSeconds(), r.Interval.Minutes, r.Source, r.ExportWh, r.ImportWh,
            r.ProductionWh, r.ConsumptionWh, r.BatteryChargeWh, r.BatteryDischargeWh, r.SelfConsumptionWh, now], ct);
    }

    /// <summary>
    /// Per ogni giorno italiano in [from, to] che ha misure del fornitore, l'ultima volta che sono state
    /// importate. Il job ci decide quali giorni rileggere.
    /// </summary>
    public async Task<IReadOnlyDictionary<DateOnly, DateTimeOffset>> LastImportByDayAsync(
        string source, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var fromUtc = ItalianTime.DayBoundsUtc(from).StartUtc;
        var toUtc = ItalianTime.DayBoundsUtc(to).EndUtc;
        var rows = await db.EnergyReadings.AsNoTracking()
            .Where(r => r.Source == source && r.TsUtc >= fromUtc && r.TsUtc < toUtc)
            .Select(r => new { r.TsUtc, r.ImportedAt })
            .ToListAsync(ct);
        return rows.GroupBy(r => ItalianTime.LocalDate(r.TsUtc))
            .ToDictionary(g => g.Key, g => g.Max(r => r.ImportedAt));
    }

    /// <summary>Le misure che iniziano nell'intervallo [from, to), di qualunque fornitore.</summary>
    public async Task<IReadOnlyList<EnergyReading>> ListAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct)
    {
        var rows = await db.EnergyReadings.AsNoTracking()
            .Where(r => r.TsUtc >= fromUtc && r.TsUtc < toUtc)
            .ToListAsync(ct);
        return rows.Select(r => new EnergyReading(new Interval(r.TsUtc, r.Resolution), r.Source, r.ExportWh, r.ImportWh,
            r.ProductionWh, r.ConsumptionWh, r.BatteryChargeWh, r.BatteryDischargeWh, r.SelfConsumptionWh)).ToList();
    }
}
