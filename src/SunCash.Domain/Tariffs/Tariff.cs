namespace SunCash.Domain.Tariffs;

/// <summary>Come l'offerta luce prezza l'energia.</summary>
public enum OfferKind
{
    /// <summary>Un prezzo fisso per ogni ora (salvato in <see cref="Tariff.EnergyF1EurKwh"/>).</summary>
    Monoraria,

    /// <summary>Un prezzo per ciascuna fascia ARERA F1, F2, F3.</summary>
    Fasce,

    /// <summary>PUN Index GME più uno spread.</summary>
    IndicizzataPun,
}

/// <summary>Quale PUN usa un'offerta indicizzata.</summary>
public enum PunBasis
{
    /// <summary>Il PUN dello stesso intervallo.</summary>
    Orario,

    /// <summary>La media mensile del PUN.</summary>
    Mensile,
}

/// <summary>Da dove è arrivata una riga inserita in SunCash (offerta o bolletta).</summary>
public enum DataOrigin
{
    /// <summary>Modulo compilato a mano: la via della prima versione e il fallback delle altre.</summary>
    Manual,

    /// <summary>Scaricata dagli open data del Portale Offerte ARERA a partire dal codice offerta.</summary>
    OfferCode,

    /// <summary>Letta dal PDF della bolletta e confermata dall'utente.</summary>
    BillPdf,
}

/// <summary>
/// L'offerta luce dell'utente, valida da <see cref="ValidFrom"/> fino alla riga successiva. Contiene
/// solo la parte che dipende dal contratto: la parte regolata è in <see cref="RegulatedCharges"/>.
/// </summary>
public sealed record Tariff(
    DateOnly ValidFrom,
    OfferKind Kind,
    decimal? EnergyF1EurKwh,
    decimal? EnergyF2EurKwh,
    decimal? EnergyF3EurKwh,
    decimal? PunSpreadEurKwh,
    PunBasis? PunBasis,
    decimal LossesFactor,
    DataOrigin Origin,
    string? OfferCode = null,
    string? Notes = null);

/// <summary>
/// Parte regolata ARERA del prezzo variabile della bolletta (uguale per tutti i clienti domestici
/// residenti), valida da <see cref="ValidFrom"/> fino alla riga successiva.
/// </summary>
public sealed record RegulatedCharges(DateOnly ValidFrom, decimal NetworkEurKwh, decimal ExciseEurKwh, decimal VatRate);
