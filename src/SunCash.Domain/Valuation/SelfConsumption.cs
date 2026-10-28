using SunCash.Domain.Energy;

namespace SunCash.Domain.Valuation;

/// <summary>
/// Energia autoconsumata = consumo − prelievo. L'impianto ha una batteria, quindi la scorciatoia
/// «prodotta − immessa» non si usa mai: sposterebbe il risparmio nell'ora di carica.
/// </summary>
public static class SelfConsumption
{
    /// <summary>Autoconsumo in Wh e la base usata per calcolarlo.</summary>
    public static (long? Wh, SelfConsumptionBasis Basis) Compute(EnergyTotals e)
    {
        if (e.ConsumptionWh is { } consumption && e.ImportWh is { } import)
            return (Math.Max(0, consumption - import), SelfConsumptionBasis.Measured);

        if (e is { ProductionWh: { } production, ImportWh: { } imp, ExportWh: { } export,
                   BatteryChargeWh: { } charge, BatteryDischargeWh: { } discharge })
        {
            var consumption2 = production + imp - export - charge + discharge;
            return (Math.Max(0, consumption2 - imp), SelfConsumptionBasis.Balance);
        }

        return (null, SelfConsumptionBasis.Unavailable);
    }
}

/// <summary>
/// Somme di energia su un intervallo di valorizzazione. Un campo è <c>null</c> se manca in almeno
/// una delle misure sommate: una somma parziale sarebbe un numero sbagliato, non un numero piccolo.
/// </summary>
public sealed record EnergyTotals(
    long? ExportWh,
    long? ImportWh,
    long? ProductionWh,
    long? ConsumptionWh,
    long? BatteryChargeWh,
    long? BatteryDischargeWh)
{
    /// <summary>Somma le misure, campo per campo.</summary>
    public static EnergyTotals Sum(IReadOnlyCollection<EnergyReading> readings) => new(
        SumOrNull(readings, r => r.ExportWh),
        SumOrNull(readings, r => r.ImportWh),
        SumOrNull(readings, r => r.ProductionWh),
        SumOrNull(readings, r => r.ConsumptionWh),
        SumOrNull(readings, r => r.BatteryChargeWh),
        SumOrNull(readings, r => r.BatteryDischargeWh));

    private static long? SumOrNull(IReadOnlyCollection<EnergyReading> readings, Func<EnergyReading, long?> field) =>
        readings.Count == 0 || readings.Any(r => field(r) is null) ? null : readings.Sum(r => field(r)!.Value);
}
