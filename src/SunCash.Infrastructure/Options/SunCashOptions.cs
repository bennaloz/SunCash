using System.ComponentModel.DataAnnotations;
using SunCash.Domain.Valuation;

namespace SunCash.Infrastructure.Options;

/// <summary>Sezione <c>Sync</c>: da quando ricostruire lo storico e se i job girano da soli.</summary>
public sealed class SyncOptions
{
    /// <summary>Nome della sezione di configurazione.</summary>
    public const string Section = "Sync";

    /// <summary>Primo giorno italiano dello storico (backfill).</summary>
    [Required]
    public DateOnly? StartDate { get; set; }

    /// <summary>Falso per non far partire i job pianificati (es. nei test dell'API).</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>Sezione <c>Valuation</c>: come si valorizza l'energia immessa.</summary>
public sealed class ValuationOptions
{
    /// <summary>Nome della sezione di configurazione.</summary>
    public const string Section = "Valuation";

    /// <summary>Regime GSE.</summary>
    public ValuationRegime Regime { get; set; } = ValuationRegime.RidZonale;

    /// <summary>Zona di mercato del prezzo zonale (es. <c>NORD</c>). Nessuna zona è cablata nel codice.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Zone { get; set; } = "";

    /// <summary>Costi fissi annui (corrispettivo GSE e altri), solo nel cumulato.</summary>
    [Range(0, double.MaxValue)]
    public decimal AnnualFixedCostsEur { get; set; }
}

/// <summary>Sezione <c>Energy</c>: quale fornitore di energia è attivo.</summary>
public sealed class EnergyOptions
{
    /// <summary>Nome della sezione di configurazione.</summary>
    public const string Section = "Energy";

    /// <summary>Nome del fornitore attivo (es. <c>FusionSolar</c>); le sue opzioni stanno in <c>Energy:&lt;Fornitore&gt;</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Provider { get; set; } = "";

    /// <summary>Quanti giorni di storico arretrato importare al massimo in un'esecuzione.</summary>
    [Range(1, 366)]
    public int MaxBackfillDaysPerRun { get; set; } = 10;
}
