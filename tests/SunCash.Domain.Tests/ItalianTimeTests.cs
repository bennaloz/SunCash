using SunCash.Domain.Time;

namespace SunCash.Domain.Tests;

public class ItalianTimeTests
{
    [Theory]
    [InlineData("2026-03-29", 92)]  // passaggio all'ora legale: 23 ore
    [InlineData("2026-06-15", 96)]
    [InlineData("2026-10-25", 100)] // ritorno all'ora solare: 25 ore
    [InlineData("2027-03-28", 92)]
    [InlineData("2027-10-31", 100)]
    public void Un_giorno_italiano_ha_il_numero_giusto_di_quarti_d_ora(string day, int expected)
    {
        Assert.Equal(expected, ItalianTime.QuarterStartsUtc(DateOnly.Parse(day)).Count);
    }

    [Fact]
    public void La_mezzanotte_italiana_d_inverno_e_alle_23_UTC_del_giorno_prima()
    {
        var (start, end) = ItalianTime.DayBoundsUtc(new DateOnly(2026, 1, 15));

        Assert.Equal(new DateTimeOffset(2026, 1, 14, 23, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 23, 0, 0, TimeSpan.Zero), end);
    }

    [Fact]
    public void La_mezzanotte_italiana_d_estate_e_alle_22_UTC_del_giorno_prima()
    {
        var (start, _) = ItalianTime.DayBoundsUtc(new DateOnly(2026, 7, 1));

        Assert.Equal(new DateTimeOffset(2026, 6, 30, 22, 0, 0, TimeSpan.Zero), start);
    }

    [Fact]
    public void Nel_ritorno_all_ora_solare_le_due_compaiono_due_volte()
    {
        var labels = ItalianTime.QuarterStartsUtc(new DateOnly(2026, 10, 25)).Select(ItalianTime.LocalLabel).ToList();

        Assert.Equal(2, labels.Count(l => l == "02:00"));
        Assert.Equal("00:00", labels[0]);
        Assert.Equal("23:45", labels[^1]);
    }

    [Fact]
    public void Nel_passaggio_all_ora_legale_le_due_non_esistono()
    {
        var labels = ItalianTime.QuarterStartsUtc(new DateOnly(2026, 3, 29)).Select(ItalianTime.LocalLabel).ToList();

        Assert.DoesNotContain("02:00", labels);
        Assert.Contains("03:00", labels);
    }

    [Fact]
    public void Ogni_quarto_d_ora_appartiene_al_suo_giorno_italiano()
    {
        var day = new DateOnly(2026, 10, 25);

        Assert.All(ItalianTime.QuarterStartsUtc(day), q => Assert.Equal(day, ItalianTime.LocalDate(q)));
    }
}
