using System.ComponentModel.DataAnnotations;

namespace SunCash.Infrastructure.Gme;

/// <summary>
/// Sezione <c>Gme</c>: accesso all'API del GME (credenziali solo da segreti o variabili d'ambiente).
/// Le credenziali sono facoltative all'avvio: arrivano giorni dopo la registrazione, e senza di
/// esse l'app deve comunque partire con i dati dell'impianto. Il job dei prezzi lo segnala in «Stato».
/// </summary>
public sealed class GmeOptions
{
    /// <summary>Nome della sezione di configurazione.</summary>
    public const string Section = "Gme";

    /// <summary>Indirizzo del servizio, con la barra finale.</summary>
    [Required]
    public Uri BaseUrl { get; set; } = new("https://api.mercatoelettrico.org/request/");

    /// <summary>Utente fornito dal GME.</summary>
    public string UserName { get; set; } = "";

    /// <summary>Password (scade ogni 6 mesi).</summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// Giorni per richiesta. Le quote contano anche i dati (oggetti × campi): una richiesta di 7 giorni
    /// ne vale circa 60 000 contro un massimo di 2 250 000 al minuto (GetMyQuotas, 2026-11-06), quindi un
    /// mese per richiesta sta largo e consuma meno connessioni, che sono la quota stretta.
    /// </summary>
    [Range(1, 31)]
    public int MaxDaysPerRequest { get; set; } = 31;

    /// <summary>
    /// Pausa minima fra due richieste al GME, login compreso. Quote dell'account lette con
    /// <c>GetMyQuotas</c> il 2026-11-06: 10 connessioni al minuto e 100 all'ora. 40 secondi fanno al
    /// massimo 90 connessioni all'ora. Il job riscrive le quote nel log a ogni esecuzione: se il GME
    /// le cambia, si ritara qui (docs/analisi.md, «Prezzi: API del GME»).
    /// </summary>
    public TimeSpan MinIntervalBetweenRequests { get; set; } = TimeSpan.FromSeconds(40);

    /// <summary>Vero se utente e password ci sono.</summary>
    public bool HasCredentials => !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);
}
