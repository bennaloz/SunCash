using System.IO.Compression;
using System.Text;
using SunCash.Domain.Prices;
using SunCash.Domain.Time;
using SunCash.Infrastructure.Gme;

namespace SunCash.Infrastructure.Tests;

public class GmeParserTests
{
    private static readonly DateOnly FallBack = new(2026, 10, 25);

    [Fact]
    public void Legge_lo_zip_in_Base64_con_il_json_dentro()
    {
        var records = GmeZonalPriceParser.ParseContentResponse(GmeFixtures.ZipBase64("gme-zonal-prices-pt15-20261025.json"));

        Assert.Equal(300, records.Count);
        var first = records[0];
        Assert.Equal(20261025, first.FlowDate);
        Assert.Equal(1, first.Period);
        Assert.Equal("NORD", first.Zone);
        Assert.Equal(100.25m, first.Price);
    }

    [Fact]
    public void Tollera_numeri_come_stringhe_virgola_decimale_e_maiuscole_diverse()
    {
        const string json = """[{"flowdate":"20261028","HOUR":"3","period":9,"market":"MGP","zone":"NORD","price":"123,45","notes":null}]""";

        var r = GmeZonalPriceParser.ParseJson(new MemoryStream(Encoding.UTF8.GetBytes(json))).Single();

        Assert.Equal(20261028, r.FlowDate);
        Assert.Equal(3, r.Hour);
        Assert.Equal(9, r.Period);
        Assert.Equal(123.45m, r.Price);
    }

    [Fact]
    public void Prezzo_vuoto_per_asta_cancellata_viene_saltato()
    {
        var records = new[] { new GmeZonalPriceRecord(20261028, 1, 1, "MGP", "NORD", null, "Auction cancelled") };

        Assert.Empty(GmePriceMapper.ToMarketPrices(records, 15, ["NORD"]));
    }

    [Fact]
    public void Nel_giorno_da_100_quarti_il_periodo_100_e_l_ultimo_quarto_d_ora()
    {
        var records = GmeZonalPriceParser.ParseContentResponse(GmeFixtures.ZipBase64("gme-zonal-prices-pt15-20261025.json"));

        var prices = GmePriceMapper.ToMarketPrices(records, 15, ["NORD", MarketPrice.PunZone]);

        Assert.Equal(200, prices.Count); // SUD scartata
        var quarters = ItalianTime.QuarterStartsUtc(FallBack);
        var last = prices.Single(p => p.Zone == "NORD" && p.Interval.StartUtc == quarters[99]);
        Assert.Equal(125m, last.EurMwh);
        Assert.Equal("23:45", ItalianTime.LocalLabel(last.Interval.StartUtc));
    }

    [Fact]
    public void Nel_giorno_da_25_ore_le_ore_3_e_4_sono_le_due_volte_delle_02()
    {
        var records = GmeZonalPriceParser.ParseContentResponse(GmeFixtures.ZipBase64("gme-zonal-prices-pt60-20261025.json"));

        var prices = GmePriceMapper.ToMarketPrices(records, 60, ["NORD"]);

        Assert.Equal(25, prices.Count);
        var hour3 = prices.Single(p => p.EurMwh == 103m);
        var hour4 = prices.Single(p => p.EurMwh == 104m);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero), hour3.Interval.StartUtc); // 02:00 ora legale
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 1, 0, 0, TimeSpan.Zero), hour4.Interval.StartUtc); // 02:00 ora solare
        Assert.Equal("02:00", ItalianTime.LocalLabel(hour3.Interval.StartUtc));
        Assert.Equal("02:00", ItalianTime.LocalLabel(hour4.Interval.StartUtc));
    }

    [Fact]
    public void Un_periodo_fuori_dal_giorno_e_un_errore_non_un_prezzo_spostato()
    {
        var records = new[] { new GmeZonalPriceRecord(20261028, 25, 97, "MGP", "NORD", 1m, null) }; // giorno da 96

        Assert.Throws<GmeException>(() => GmePriceMapper.ToMarketPrices(records, 15, ["NORD"]));
    }
}

/// <summary>Fixture GME impacchettate come le restituisce RequestData.</summary>
internal static class GmeFixtures
{
    public static string ZipBase64(string fixture) => ZipBase64(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture)));

    public static string ZipBase64(byte[] json)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var entry = zip.CreateEntry("data.json").Open();
            entry.Write(json);
        }

        return Convert.ToBase64String(buffer.ToArray());
    }
}
