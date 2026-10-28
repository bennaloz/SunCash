namespace SunCash.Domain.Calendar;

/// <summary>Assegna a un'ora italiana la sua fascia ARERA.</summary>
public static class TariffBands
{
    /// <summary>La fascia dell'istante in ora italiana.</summary>
    public static TariffBand Of(DateTime localTime)
    {
        var date = DateOnly.FromDateTime(localTime);
        if (localTime.DayOfWeek == DayOfWeek.Sunday || ItalianHolidays.IsHoliday(date))
            return TariffBand.F3;

        var hour = localTime.Hour;
        if (hour < 7 || hour >= 23)
            return TariffBand.F3;

        if (localTime.DayOfWeek == DayOfWeek.Saturday)
            return TariffBand.F2;

        return hour is >= 8 and < 19 ? TariffBand.F1 : TariffBand.F2;
    }
}
