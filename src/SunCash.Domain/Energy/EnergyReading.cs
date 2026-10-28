using SunCash.Domain.Time;

namespace SunCash.Domain.Energy;

/// <summary>
/// Una misura di energia dell'impianto su un intervallo, nel formato comune a tutti i fornitori.
/// Energia in Wh; un campo è <c>null</c> se il fornitore non lo dà. <see cref="SelfConsumptionWh"/> è
/// l'autoconsumo come lo calcola il fornitore, quando lo dichiara: serve se mancano consumo e prelievo.
/// </summary>
public sealed record EnergyReading(
    Interval Interval,
    string Source,
    long? ExportWh,
    long? ImportWh,
    long? ProductionWh,
    long? ConsumptionWh,
    long? BatteryChargeWh,
    long? BatteryDischargeWh,
    long? SelfConsumptionWh = null);
