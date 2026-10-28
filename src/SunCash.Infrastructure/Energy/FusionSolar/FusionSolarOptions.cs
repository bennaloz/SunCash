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

    /// <summary>Codice dell'impianto, ricavato al primo login. Mai nel repo.</summary>
    [Required(AllowEmptyStrings = false)]
    public string StationCode { get; set; } = "";

    /// <summary>Intervallo minimo fra due chiamate allo stesso endpoint. Prudente: va tarato sull'account.</summary>
    public TimeSpan MinIntervalPerEndpoint { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Nomi dei campi KPI orari in <c>dataItemMap</c>.</summary>
    public FusionSolarFields Fields { get; set; } = new();
}

/// <summary>
/// Nomi dei campi di <c>getKpiStationHour</c>, in kWh. Un campo vuoto = grandezza non letta.
/// DA VERIFICARE (docs/analisi.md, «Primo fornitore: Huawei FusionSolar»): i valori predefiniti sono
/// candidati, non verificati. Il client registra nel log le chiavi che riceve davvero: si confrontano
/// con la prima risposta reale e si correggono qui o in configurazione.
/// </summary>
public sealed class FusionSolarFields
{
    /// <summary>Energia immessa in rete.</summary>
    public string Export { get; set; } = "ongrid_power";
    /// <summary>Energia prelevata dalla rete.</summary>
    public string Import { get; set; } = "buyPower";
    /// <summary>Energia prodotta.</summary>
    public string Production { get; set; } = "inverter_power";
    /// <summary>Consumo della casa.</summary>
    public string Consumption { get; set; } = "use_power";
    /// <summary>Energia caricata in batteria.</summary>
    public string BatteryCharge { get; set; } = "chargeCap";
    /// <summary>Energia scaricata dalla batteria.</summary>
    public string BatteryDischarge { get; set; } = "dischargeCap";
}
