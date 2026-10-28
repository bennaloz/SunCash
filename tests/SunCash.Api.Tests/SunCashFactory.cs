using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace SunCash.Api.Tests;

/// <summary>
/// L'app vera, con un database SQLite tutto suo su file temporaneo e i job pianificati spenti.
/// Una per test: niente stato condiviso fra test che girano in parallelo.
/// </summary>
public sealed class SunCashFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"suncash-api-{Guid.NewGuid():N}.db");

    /// <summary>Orologio dell'app, spostabile dal test.</summary>
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 28, 11, 0, 0, TimeSpan.Zero));

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:SunCash", $"Data Source={_dbPath};Pooling=False");
        builder.UseSetting("Sync:Enabled", "false");
        builder.UseSetting("Sync:StartDate", "2026-01-01");
        builder.UseSetting("Valuation:Zone", "NORD");
        builder.UseSetting("Energy:Provider", "FusionSolar");
        builder.UseSetting("Energy:FusionSolar:BaseUrl", "http://localhost:1/thirdData/");
        builder.UseSetting("Energy:FusionSolar:UserName", "utente");
        builder.UseSetting("Energy:FusionSolar:SystemCode", "segreto");
        builder.UseSetting("Energy:FusionSolar:StationCode", "NE=00000000");
        builder.UseSetting("Gme:UserName", "utente");
        builder.UseSetting("Gme:Password", "segreto");
        builder.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock)));
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        foreach (var file in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            if (File.Exists(file))
                File.Delete(file);
    }
}
