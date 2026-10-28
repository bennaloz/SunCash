using System.ComponentModel.DataAnnotations;

namespace SunCash.Infrastructure.DbBackup;

/// <summary>Sezione <c>Backup</c>: dove e per quanto tenere le copie giornaliere del database.</summary>
public sealed class BackupOptions
{
    /// <summary>Nome della sezione di configurazione.</summary>
    public const string Section = "Backup";

    /// <summary>Cartella delle copie (nel container, un volume su disco locale). Vuota = backup spento.</summary>
    public string? Directory { get; set; }

    /// <summary>Quante copie giornaliere tenere.</summary>
    [Range(1, 365)]
    public int KeepDays { get; set; } = 14;
}
