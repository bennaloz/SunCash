using System.Xml.Linq;

namespace SunCash.Domain.Tests;

/// <summary>
/// Guardia: SunCash.Domain non dipende da nessun pacchetto e da nessun altro progetto,
/// così il calcolo resta testabile da solo (CLAUDE.md, «Architettura in breve»).
/// </summary>
public class DomainSenzaDipendenzeTests
{
    private static readonly string[] ElementiVietati = ["PackageReference", "ProjectReference", "Reference", "FrameworkReference"];

    [Fact]
    public void Il_progetto_di_dominio_non_dichiara_dipendenze()
    {
        var csproj = XDocument.Load(PercorsoCsprojDominio());

        var dipendenze = csproj.Descendants()
            .Where(e => ElementiVietati.Contains(e.Name.LocalName))
            .Select(e => $"{e.Name.LocalName} {e.Attribute("Include")?.Value}")
            .ToList();

        Assert.Empty(dipendenze);
    }

    [Fact]
    public void Il_controllo_riconosce_una_dipendenza_quando_c_e()
    {
        // Controcampione: se il controllo smettesse di vedere le dipendenze, il test sopra
        // sarebbe verde senza guardare niente.
        var campione = XDocument.Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup><PackageReference Include="Qualcosa" /></ItemGroup>
            </Project>
            """);

        Assert.Contains(campione.Descendants(), e => ElementiVietati.Contains(e.Name.LocalName));
    }

    private static string PercorsoCsprojDominio()
    {
        var cartella = new DirectoryInfo(AppContext.BaseDirectory);
        while (cartella is not null && !File.Exists(Path.Combine(cartella.FullName, "SunCash.slnx")))
            cartella = cartella.Parent;

        Assert.NotNull(cartella);
        var percorso = Path.Combine(cartella.FullName, "src", "SunCash.Domain", "SunCash.Domain.csproj");
        Assert.True(File.Exists(percorso), $"Non trovo {percorso}: la guardia non sta guardando niente.");
        return percorso;
    }
}
