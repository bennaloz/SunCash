namespace SunCash.Domain.Valuation;

/// <summary>Granularità con cui è stata valorizzata una riga.</summary>
public enum Resolution
{
    /// <summary>Energia a 15 minuti (o a 5 aggregata) per il prezzo del quarto d'ora.</summary>
    Quarter,

    /// <summary>Solo il dato orario: energia e prezzo dell'ora come blocco unico.</summary>
    Hour,
}

/// <summary>Da dove viene il consumo con cui si calcola l'autoconsumo.</summary>
public enum SelfConsumptionBasis
{
    /// <summary>Consumo della casa misurato dal fornitore.</summary>
    Measured,

    /// <summary>Consumo ricavato dal bilancio: prodotta + prelevata − immessa − carica + scarica.</summary>
    Balance,

    /// <summary>Dati insufficienti: il risparmio non si calcola, invece di mostrarne uno sbagliato.</summary>
    Unavailable,
}

/// <summary>Regola con cui il GSE valorizza l'energia immessa (<c>Valuation:Regime</c>).</summary>
public enum ValuationRegime
{
    /// <summary>Ritiro Dedicato a prezzo zonale.</summary>
    RidZonale,

    /// <summary>Ritiro Dedicato con prezzo minimo garantito: vale la serie più favorevole nell'anno.</summary>
    RidPmg,
}
