using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace SunCash.Infrastructure.DbBackup;

/// <summary>
/// All'avvio, prima di aprire il database: se il file non c'è e c'è una copia, riparte dalla più
/// recente. Serve dove il disco del database non sopravvive a un riavvio (Azure Container Apps):
/// offerte, bollette e storico tornano dall'ultimo backup, il resto lo reimportano i job.
/// </summary>
public static class DatabaseRestore
{
    /// <summary>Ripristina se serve; restituisce la copia usata, o <c>null</c>.</summary>
    public static string? RestoreIfMissing(string connectionString, string? backupDirectory, ILogger logger)
    {
        var database = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(backupDirectory) || string.IsNullOrWhiteSpace(database) || File.Exists(database))
            return null;

        var latest = BackupFiles.List(backupDirectory).FirstOrDefault();
        if (latest.Path is null)
        {
            logger.LogInformation("Database assente e nessuna copia in {Directory}: si parte da zero.", backupDirectory);
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(database))!);
        File.Copy(latest.Path, database);
        logger.LogWarning("Database assente: ripristinato dalla copia del {Day:yyyy-MM-dd} ({Path}).", latest.Day, latest.Path);
        return latest.Path;
    }
}
