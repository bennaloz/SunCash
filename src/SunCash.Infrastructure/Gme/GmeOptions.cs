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

    /// <summary>Giorni per richiesta: le quote del GME contano oggetti × campi. Valore prudente.</summary>
    [Range(1, 31)]
    public int MaxDaysPerRequest { get; set; } = 7;

    /// <summary>Vero se utente e password ci sono.</summary>
    public bool HasCredentials => !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);
}
