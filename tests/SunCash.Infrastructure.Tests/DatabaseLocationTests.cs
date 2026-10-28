using Microsoft.Extensions.Configuration;
using SunCash.Infrastructure.Persistence;

namespace SunCash.Infrastructure.Tests;

public class DatabaseLocationTests
{
    [Fact]
    public void I_percorsi_espandono_le_variabili_d_ambiente_come_su_App_Service()
    {
        // Variabile con nome unico: l'ambiente del processo è condiviso fra test in parallelo.
        var name = $"SUNCASH_TEST_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(name, "/percorso/di/prova");
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SunCash"] = $"Data Source=%{name}%/suncash.db",
                ["Backup:Directory"] = $"%{name}%/backups",
            }).Build();

            Assert.Equal("Data Source=/percorso/di/prova/suncash.db", DatabaseLocation.ConnectionString(configuration));
            Assert.Equal("/percorso/di/prova/backups", DatabaseLocation.BackupDirectory(configuration));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void Crea_la_cartella_del_database_se_manca()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"suncash-location-{Guid.NewGuid():N}", "dati");
        try
        {
            DatabaseLocation.EnsureDirectory($"Data Source={Path.Combine(directory, "suncash.db")}");

            Assert.True(Directory.Exists(directory));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(directory)!, recursive: true);
        }
    }
}
