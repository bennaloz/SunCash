using SunCash.Domain.Time;

namespace SunCash.Domain.Calendar;

/// <summary>Costruisce il calendario dei quarti d'ora per giorni italiani.</summary>
public static class TimeSlotCalendar
{
    /// <summary>I quarti d'ora di un giorno italiano, in ordine.</summary>
    public static IReadOnlyList<TimeSlot> ForDay(DateOnly localDate) =>
        ItalianTime.QuarterStartsUtc(localDate)
            .Select(start =>
            {
                var local = ItalianTime.ToLocal(start);
                return new TimeSlot(start, localDate, local.ToString("HH:mm"), TariffBands.Of(local));
            })
            .ToList();

    /// <summary>I quarti d'ora di un intervallo di giorni italiani, estremi inclusi.</summary>
    public static IEnumerable<TimeSlot> ForDays(DateOnly fromInclusive, DateOnly toInclusive)
    {
        for (var day = fromInclusive; day <= toInclusive; day = day.AddDays(1))
            foreach (var slot in ForDay(day))
                yield return slot;
    }
}
