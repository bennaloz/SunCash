using SunCash.Infrastructure.Persistence;

namespace SunCash.Infrastructure.Tests;

public class RegulatedChargesSeedTests
{
    [Fact]
    public void Il_file_del_repo_si_legge_e_ha_valori_plausibili()
    {
        var charges = RegulatedChargesSeed.Load();

        Assert.NotEmpty(charges);
        Assert.Equal(charges.Count, charges.Select(c => c.ValidFrom).Distinct().Count());
        // Errori tipici: centesimi scritti come euro (4,5 invece di 0,045) o IVA in percento (10 invece di 0,10).
        Assert.All(charges, c =>
        {
            Assert.InRange(c.NetworkEurKwh, 0.01m, 0.15m);
            Assert.InRange(c.ExciseEurKwh, 0m, 0.05m);
            Assert.InRange(c.VatRate, 0m, 0.25m);
        });
    }

    [Fact]
    public void Dal_1_luglio_2026_valgono_gli_oneri_della_delibera_227_2026()
    {
        var july = RegulatedChargesSeed.Load().Single(c => c.ValidFrom == new DateOnly(2026, 7, 1));

        // TD 1,190 + UC3 0,276 + UC6 0,007 + ASOS 3,1515 + ARIM 0,1638 centesimi/kWh (docs/analisi.md).
        Assert.Equal(0.047883m, july.NetworkEurKwh);
    }
}
