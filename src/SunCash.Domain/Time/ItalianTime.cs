namespace SunCash.Domain.Time;

/// <summary>
/// L'unico punto che conosce l'ora italiana: conversioni fra UTC e ora locale con il fuso
/// <c>Europe/Rome</c>, mai con offset fissi. Il GME, le fasce ARERA e le date della dashboard
/// ragionano in ora italiana; il database ragiona in UTC.
/// </summary>
public static class ItalianTime
{
    /// <summary>Durata di un quarto d'ora, l'unità minima di valorizzazione.</summary>
    public const int QuarterMinutes = 15;

    /// <summary>Il fuso orario italiano, con ora legale.</summary>
    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    /// <summary>Inizio (incluso) e fine (esclusa) in UTC di un giorno in ora italiana.</summary>
    public static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) DayBoundsUtc(DateOnly localDate) =>
        (MidnightUtc(localDate), MidnightUtc(localDate.AddDays(1)));

    /// <summary>
    /// Gli inizi UTC dei quarti d'ora di un giorno italiano: 96 di solito, 92 il giorno del
    /// passaggio all'ora legale, 100 quello del ritorno all'ora solare.
    /// </summary>
    public static IReadOnlyList<DateTimeOffset> QuarterStartsUtc(DateOnly localDate)
    {
        var (start, end) = DayBoundsUtc(localDate);
        var quarters = new List<DateTimeOffset>(100);
        for (var t = start; t < end; t = t.AddMinutes(QuarterMinutes))
            quarters.Add(t);
        return quarters;
    }

    /// <summary>Data e ora italiane di un istante UTC.</summary>
    public static DateTime ToLocal(DateTimeOffset instantUtc) =>
        TimeZoneInfo.ConvertTimeFromUtc(instantUtc.UtcDateTime, Zone);

    /// <summary>Il giorno italiano a cui appartiene un istante UTC.</summary>
    public static DateOnly LocalDate(DateTimeOffset instantUtc) => DateOnly.FromDateTime(ToLocal(instantUtc));

    /// <summary>Etichetta <c>HH:mm</c> in ora italiana; nel ritorno all'ora solare le 02:xx compaiono due volte.</summary>
    public static string LocalLabel(DateTimeOffset instantUtc) => ToLocal(instantUtc).ToString("HH:mm");

    /// <summary>Inizio UTC dell'ora che contiene l'istante. L'Italia dista da UTC un numero intero
    /// di ore, quindi l'ora UTC e l'ora italiana coincidono come intervallo.</summary>
    public static DateTimeOffset HourStartUtc(DateTimeOffset instantUtc)
    {
        var utc = instantUtc.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }

    private static DateTimeOffset MidnightUtc(DateOnly localDate)
    {
        // La mezzanotte italiana esiste sempre: i cambi d'ora avvengono alle 02:00 e alle 03:00.
        var local = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zone), TimeSpan.Zero);
    }
}
