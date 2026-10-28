using SunCash.Domain.Tariffs;

namespace SunCash.Infrastructure.Persistence;

/// <summary>Riga di <c>market_prices</c>: un prezzo GME grezzo.</summary>
public sealed class MarketPriceRow
{
    /// <summary>Inizio dell'intervallo (epoch UTC in secondi nel database).</summary>
    public DateTimeOffset TsUtc { get; set; }
    /// <summary>Minuti: 15 o 60.</summary>
    public int Resolution { get; set; }
    /// <summary>Mercato, <c>MGP</c>.</summary>
    public string Market { get; set; } = "";
    /// <summary>Zona di mercato o <c>PUN</c>.</summary>
    public string Zone { get; set; } = "";
    /// <summary>Prezzo in €/MWh.</summary>
    public decimal EurMwh { get; set; }
    /// <summary>Quando è stato importato.</summary>
    public DateTimeOffset ImportedAt { get; set; }
}

/// <summary>Riga di <c>energy_readings</c>: una misura grezza di un fornitore.</summary>
public sealed class EnergyReadingRow
{
    /// <summary>Inizio dell'intervallo.</summary>
    public DateTimeOffset TsUtc { get; set; }
    /// <summary>Minuti: 5, 15 o 60.</summary>
    public int Resolution { get; set; }
    /// <summary>Id del fornitore.</summary>
    public string Source { get; set; } = "";
    /// <summary>Energia immessa in Wh.</summary>
    public long? ExportWh { get; set; }
    /// <summary>Energia prelevata in Wh.</summary>
    public long? ImportWh { get; set; }
    /// <summary>Energia prodotta in Wh.</summary>
    public long? ProductionWh { get; set; }
    /// <summary>Consumo della casa in Wh.</summary>
    public long? ConsumptionWh { get; set; }
    /// <summary>Energia caricata in batteria in Wh.</summary>
    public long? BatteryChargeWh { get; set; }
    /// <summary>Energia scaricata dalla batteria in Wh.</summary>
    public long? BatteryDischargeWh { get; set; }
    /// <summary>Autoconsumo dichiarato dal fornitore in Wh.</summary>
    public long? SelfConsumptionWh { get; set; }
    /// <summary>Quando è stata importata.</summary>
    public DateTimeOffset ImportedAt { get; set; }
}

/// <summary>Riga di <c>tariffs</c>: l'offerta luce in un periodo.</summary>
public sealed class TariffRow
{
    /// <summary>Inizio validità, giorno italiano.</summary>
    public DateOnly ValidFrom { get; set; }
    /// <summary>Tipo di offerta.</summary>
    public OfferKind Kind { get; set; }
    /// <summary>Prezzo unico (monoraria) o F1, €/kWh.</summary>
    public decimal? EnergyF1EurKwh { get; set; }
    /// <summary>Prezzo F2, €/kWh.</summary>
    public decimal? EnergyF2EurKwh { get; set; }
    /// <summary>Prezzo F3, €/kWh.</summary>
    public decimal? EnergyF3EurKwh { get; set; }
    /// <summary>Spread sul PUN, €/kWh.</summary>
    public decimal? PunSpreadEurKwh { get; set; }
    /// <summary>PUN orario o mensile.</summary>
    public PunBasis? PunBasis { get; set; }
    /// <summary>Perdite di rete (es. 0,102).</summary>
    public decimal LossesFactor { get; set; }
    /// <summary>Da dove è arrivata la riga.</summary>
    public DataOrigin Origin { get; set; }
    /// <summary>Codice offerta, se noto.</summary>
    public string? OfferCode { get; set; }
    /// <summary>Note libere.</summary>
    public string? Notes { get; set; }
}

/// <summary>Riga di <c>regulated_charges</c>: parte regolata ARERA in un periodo.</summary>
public sealed class RegulatedChargesRow
{
    /// <summary>Inizio validità.</summary>
    public DateOnly ValidFrom { get; set; }
    /// <summary>Trasporto e oneri, quota variabile, €/kWh.</summary>
    public decimal NetworkEurKwh { get; set; }
    /// <summary>Accise, €/kWh.</summary>
    public decimal ExciseEurKwh { get; set; }
    /// <summary>Aliquota IVA.</summary>
    public decimal VatRate { get; set; }
}

/// <summary>Riga di <c>bills</c>: una bolletta reale.</summary>
public sealed class BillRow
{
    /// <summary>Primo giorno del periodo fatturato.</summary>
    public DateOnly PeriodFrom { get; set; }
    /// <summary>Ultimo giorno del periodo fatturato, incluso.</summary>
    public DateOnly PeriodTo { get; set; }
    /// <summary>Prelievi fatturati in kWh.</summary>
    public decimal? ImportKwh { get; set; }
    /// <summary>Totale della bolletta in euro.</summary>
    public decimal AmountEur { get; set; }
    /// <summary>Da dove è arrivata la riga.</summary>
    public DataOrigin Origin { get; set; }
    /// <summary>Note libere.</summary>
    public string? Notes { get; set; }
}

/// <summary>Riga di <c>price_floors</c>: prezzo minimo garantito di un anno.</summary>
public sealed class PriceFloorRow
{
    /// <summary>Anno.</summary>
    public int Year { get; set; }
    /// <summary>€/MWh.</summary>
    public decimal EurMwh { get; set; }
}

/// <summary>Riga di <c>gse_payments</c>: un pagamento GSE inserito a mano.</summary>
public sealed class GsePaymentRow
{
    /// <summary>Mese di competenza, <c>yyyy-MM</c>.</summary>
    public string Month { get; set; } = "";
    /// <summary>Importo in euro.</summary>
    public decimal AmountEur { get; set; }
    /// <summary>Note libere.</summary>
    public string? Notes { get; set; }
}

/// <summary>Riga di <c>sync_state</c>: stato di un job di sincronizzazione.</summary>
public sealed class SyncStateRow
{
    /// <summary>Nome del job.</summary>
    public string Job { get; set; } = "";
    /// <summary>Per l'import di energia, il fornitore attivo.</summary>
    public string? Source { get; set; }
    /// <summary>Ultima esecuzione riuscita.</summary>
    public DateTimeOffset? LastSuccessUtc { get; set; }
    /// <summary>Primo giorno italiano non ancora completo nello storico.</summary>
    public DateOnly? Cursor { get; set; }
    /// <summary>Ultimo errore, vuoto dopo un successo.</summary>
    public string? LastError { get; set; }
    /// <summary>Ultimo aggiornamento della riga.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
