using SunCash.Domain.Calendar;

namespace SunCash.Domain.Tests;

public class TariffBandsTests
{
    [Theory]
    [InlineData("2026-10-28T10:00", TariffBand.F1)] // mercoledì
    [InlineData("2026-10-28T08:00", TariffBand.F1)]
    [InlineData("2026-10-28T18:45", TariffBand.F1)]
    [InlineData("2026-10-28T07:30", TariffBand.F2)]
    [InlineData("2026-10-28T19:00", TariffBand.F2)]
    [InlineData("2026-10-28T22:45", TariffBand.F2)]
    [InlineData("2026-10-28T23:00", TariffBand.F3)]
    [InlineData("2026-10-28T06:45", TariffBand.F3)]
    [InlineData("2026-10-31T10:00", TariffBand.F2)] // sabato
    [InlineData("2026-10-31T23:15", TariffBand.F3)]
    [InlineData("2026-11-01T10:00", TariffBand.F3)] // domenica e Ognissanti
    [InlineData("2026-04-06T10:00", TariffBand.F3)] // lunedì dell'Angelo 2026
    [InlineData("2026-12-08T12:00", TariffBand.F3)] // Immacolata, martedì
    public void Fascia_ARERA(string localTime, TariffBand expected)
    {
        Assert.Equal(expected, TariffBands.Of(DateTime.Parse(localTime)));
    }
}

public class ItalianHolidaysTests
{
    [Theory]
    [InlineData(2026, "2026-04-05")]
    [InlineData(2027, "2027-03-28")]
    [InlineData(2028, "2028-04-16")]
    public void Pasqua(int year, string expected)
    {
        Assert.Equal(DateOnly.Parse(expected), ItalianHolidays.EasterSunday(year));
    }

    [Fact]
    public void Il_lunedi_dell_Angelo_e_festivo_e_il_martedi_no()
    {
        Assert.True(ItalianHolidays.IsHoliday(new DateOnly(2027, 3, 29)));
        Assert.False(ItalianHolidays.IsHoliday(new DateOnly(2027, 3, 30)));
    }

    [Fact]
    public void Il_4_ottobre_non_e_ancora_festivo_per_le_fasce()
    {
        // DA VERIFICARE (docs/analisi.md, «Fasce»): se ARERA lo aggiunge, questo test va invertito.
        Assert.False(ItalianHolidays.IsHoliday(new DateOnly(2027, 10, 4)));
    }
}
