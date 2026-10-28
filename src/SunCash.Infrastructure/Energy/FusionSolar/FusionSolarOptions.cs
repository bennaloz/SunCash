using System.ComponentModel.DataAnnotations;

namespace SunCash.Infrastructure.Energy.FusionSolar;

/// <summary>Sezione <c>Energy:FusionSolar</c>: account Northbound (credenziali solo da segreti o variabili d'ambiente).</summary>
public sealed class FusionSolarOptions
{
    /// <summary>Nome della sezione di configurazione.</summary>
    public const string Section = "Energy:FusionSolar";

    /// <summary>Es. <c>https://&lt;prefisso&gt;.fusionsolar.huawei.com/thirdData/</c>, con la barra finale.</summary>
    [Required]
    public Uri? BaseUrl { get; set; }

    /// <summary>Utente Northbound.</summary>
    [Required(AllowEmptyStrings = false)]
    public string UserName { get; set; } = "";

    /// <summary>Password Northbound («systemCode»).</summary>
    [Required(AllowEmptyStrings = false)]
    public string SystemCode { get; set; } = "";

    /// <summary>
    /// Codice dell'impianto («NE=…»). Facoltativo: se manca, l'app usa l'unico impianto dell'account
    /// (<see cref="FusionSolarStationLocator"/>). Mai nel repo.
    /// </summary>
    public string StationCode { get; set; } = "";

    /// <summary>Intervallo minimo fra due chiamate allo stesso endpoint. Prudente: va tarato sull'account.</summary>
    public TimeSpan MinIntervalPerEndpoint { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Nomi dei campi KPI orari in <c>dataItemMap</c>.</summary>
    public FusionSolarFields Fields { get; set; } = new();
}

/// <summary>
/// Nomi dei campi di <c>getKpiStationHour</c>, in kWh. Un campo vuoto = grandezza non letta.
/// Verificato il 2026-10-28 sulla prima risposta reale (docs/analisi.md, «Primo fornitore: Huawei
/// FusionSolar»): le chiavi orarie sono <c>PVYield</c>, <c>inverter_power</c>, <c>ongrid_power</c>,
/// <c>chargeCap</c>, <c>dischargeCap</c>, <c>selfProvide</c> e altre; prelievo e consumo della casa
/// non ci sono. Il client registra comunque nel log le chiavi che riceve.
/// </summary>
public sealed class FusionSolarFields
{
    /// <summary>Energia immessa in rete.</summary>
    public string Export { get; set; } = "ongrid_power";
    /// <summary>Energia prelevata dalla rete: non c'è nei KPI orari dell'impianto.</summary>
    public string Import { get; set; } = "";
    /// <summary>Energia prodotta dai pannelli. <c>inverter_power</c> è l'uscita dell'inverter, che con una
    /// batteria ibrida può comprendere anche la scarica: nel bilancio la conterebbe due volte.</summary>
    public string Production { get; set; } = "PVYield";
    /// <summary>Consumo della casa: non c'è nei KPI orari dell'impianto.</summary>
    public string Consumption { get; set; } = "";
    /// <summary>Energia caricata in batteria.</summary>
    public string BatteryCharge { get; set; } = "chargeCap";
    /// <summary>Energia scaricata dalla batteria.</summary>
    public string BatteryDischarge { get; set; } = "dischargeCap";
    /// <summary>Autoconsumo calcolato da FusionSolar. DA VERIFICARE (docs/analisi.md): se comprende la
    /// scarica della batteria; il fornitore scrive nel log il confronto giornaliero con il bilancio.</summary>
    public string SelfConsumption { get; set; } = "selfProvide";
}
