using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using SunCash.Infrastructure.DbBackup;

namespace SunCash.Infrastructure.Persistence;

/// <summary>
/// Dove stanno il database e i backup. I percorsi in configurazione possono usare variabili
/// d'ambiente (<c>%HOME%</c>, <c>%TEMP%</c>): su Azure App Service il disco locale (<c>%TEMP%</c>)
/// e quello persistente (<c>%HOME%</c>) cambiano da un'istanza all'altra, e si scrivono così.
/// </summary>
public static class DatabaseLocation
{
    /// <summary>La stringa di connessione SQLite, con le variabili d'ambiente espanse.</summary>
    public static string ConnectionString(IConfiguration configuration) =>
        Environment.ExpandEnvironmentVariables(configuration.GetConnectionString("SunCash")
            ?? throw new InvalidOperationException("Manca ConnectionStrings:SunCash."));

    /// <summary>La cartella dei backup con le variabili espanse, o <c>null</c> se il backup è spento.</summary>
    public static string? BackupDirectory(IConfiguration configuration) =>
        configuration[$"{BackupOptions.Section}:Directory"] is { Length: > 0 } directory
            ? Environment.ExpandEnvironmentVariables(directory)
            : null;

    /// <summary>Crea la cartella del file di database, se non c'è: SQLite crea il file, non le cartelle.</summary>
    public static void EnsureDirectory(string connectionString)
    {
        var file = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(file) || file == ":memory:")
            return;
        var directory = Path.GetDirectoryName(Path.GetFullPath(file));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }
}
