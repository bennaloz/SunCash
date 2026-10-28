namespace SunCash.Domain.Calendar;

/// <summary>
/// Festività nazionali che le fasce ARERA trattano come domeniche (tutto il giorno in F3).
/// </summary>
public static class ItalianHolidays
{
    // DA VERIFICARE (docs/analisi.md, «Fasce»): il 4 ottobre (San Francesco) è festa nazionale
    // dal 2026, ma non risulta ancora se ARERA lo abbia aggiunto ai festivi delle fasce. Finché
    // non è verificato resta fuori, e il test ItalianHolidaysTests lo documenta.
    private static readonly (int Month, int Day)[] FixedHolidays =
    [
        (1, 1),   // Capodanno
        (1, 6),   // Epifania
        (4, 25),  // Liberazione
        (5, 1),   // Festa del lavoro
        (6, 2),   // Festa della Repubblica
        (8, 15),  // Ferragosto
        (11, 1),  // Ognissanti
        (12, 8),  // Immacolata
        (12, 25), // Natale
        (12, 26), // Santo Stefano
    ];

    /// <summary>Vero se il giorno è festivo per le fasce ARERA (le domeniche sono gestite a parte).</summary>
    public static bool IsHoliday(DateOnly date) =>
        FixedHolidays.Contains((date.Month, date.Day)) || date == EasterSunday(date.Year).AddDays(1);

    /// <summary>Domenica di Pasqua (calendario gregoriano, algoritmo di Meeus/Jones/Butcher).</summary>
    public static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }
}
