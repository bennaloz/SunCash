namespace SunCash.Domain.Time;

/// <summary>
/// Un intervallo di tempo che inizia in un istante UTC e dura un numero intero di minuti
/// (5, 15 o 60 nei dati di SunCash).
/// </summary>
public readonly record struct Interval(DateTimeOffset StartUtc, int Minutes)
{
    /// <summary>Fine dell'intervallo, esclusa.</summary>
    public DateTimeOffset EndUtc => StartUtc.AddMinutes(Minutes);

    /// <summary>Vero se l'istante cade nell'intervallo (inizio incluso, fine esclusa).</summary>
    public bool Contains(DateTimeOffset instantUtc) => instantUtc >= StartUtc && instantUtc < EndUtc;
}
