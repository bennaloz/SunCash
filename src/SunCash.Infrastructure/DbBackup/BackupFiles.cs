using System.Globalization;

namespace SunCash.Infrastructure.DbBackup;

/// <summary>
/// L'unico posto che sa come si chiamano le copie del database (<c>suncash-yyyyMMdd.db</c>): lo
/// usano sia il backup sia il ripristino all'avvio.
/// </summary>
public static class BackupFiles
{
    private const string Prefix = "suncash-";

    /// <summary>Percorso della copia di un giorno.</summary>
    public static string PathFor(string directory, DateOnly day) =>
        Path.Combine(directory, $"{Prefix}{day.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.db");

    /// <summary>Le copie presenti nella cartella, dalla più recente; gli altri file si ignorano.</summary>
    public static IReadOnlyList<(DateOnly Day, string Path)> List(string directory)
    {
        if (!Directory.Exists(directory))
            return [];

        var copies = new List<(DateOnly, string)>();
        foreach (var file in Directory.EnumerateFiles(directory, $"{Prefix}*.db"))
            if (DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(file)[Prefix.Length..], "yyyyMMdd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                copies.Add((day, file));
        return copies.OrderByDescending(c => c.Item1).ToList();
    }
}
