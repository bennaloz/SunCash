using SunCash.Domain.Time;

namespace SunCash.Domain.Calendar;

/// <summary>
/// Un quarto d'ora del calendario: istante UTC con la data, l'etichetta e la fascia italiane già
/// calcolate. È la riga di <c>time_slots</c>: SQLite non conosce i fusi, quindi le view leggono
/// queste informazioni da qui invece di calcolarle.
/// </summary>
public sealed record TimeSlot(DateTimeOffset StartUtc, DateOnly LocalDate, string LocalLabel, TariffBand Band)
{
    /// <summary>L'intervallo di quindici minuti del quarto d'ora.</summary>
    public Interval Interval => new(StartUtc, ItalianTime.QuarterMinutes);
}
