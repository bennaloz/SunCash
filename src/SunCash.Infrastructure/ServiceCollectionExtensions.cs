using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SunCash.Domain.Energy;
using SunCash.Infrastructure.DbBackup;
using SunCash.Infrastructure.Energy;
using SunCash.Infrastructure.Energy.FusionSolar;
using SunCash.Infrastructure.Gme;
using SunCash.Infrastructure.Options;
using SunCash.Infrastructure.Persistence;
using SunCash.Infrastructure.Sync;
using SunCash.Infrastructure.Valuation;

namespace SunCash.Infrastructure;

/// <summary>Registrazione dei servizi di SunCash.Infrastructure.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Database, client esterni, fornitore di energia attivo, job e lettura della valorizzazione.
    /// Le opzioni si validano all'avvio: una configurazione sbagliata ferma l'app subito.
    /// </summary>
    public static IServiceCollection AddSunCashInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingletonTimeProvider();

        services.AddOptions<SyncOptions>().Bind(configuration.GetSection(SyncOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ValuationOptions>().Bind(configuration.GetSection(ValuationOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<EnergyOptions>().Bind(configuration.GetSection(EnergyOptions.Section)).ValidateDataAnnotations()
            .Validate(o => EnergyProviders.Contains(o.Provider), $"Energy:Provider deve essere uno di: {string.Join(", ", EnergyProviders)}.")
            .ValidateOnStart();
        services.AddOptions<GmeOptions>().Bind(configuration.GetSection(GmeOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<BackupOptions>().Bind(configuration.GetSection(BackupOptions.Section))
            .PostConfigure(o => o.Directory = DatabaseLocation.BackupDirectory(configuration))
            .ValidateDataAnnotations().ValidateOnStart();

        services.AddDbContext<SunCashDbContext>(o => o.UseSqlite(DatabaseLocation.ConnectionString(configuration)));
        services.AddScoped<MarketPriceStore>();
        services.AddScoped<EnergyReadingStore>();
        services.AddScoped<SyncStateStore>();
        services.AddScoped<ManualDataStore>();
        services.AddScoped<ValuationReader>();

        services.AddHttpClient<GmeClient>();
        services.AddScoped<ISyncJob, GmePricesJob>();
        services.AddScoped<ISyncJob, EnergyImportJob>();
        services.AddScoped<ISyncJob, DatabaseBackupJob>();
        services.AddSingleton<SyncJobRunner>();

        AddEnergyProvider(services, configuration);
        return services;
    }

    /// <summary>Fa girare i job da soli (non nei test dell'API).</summary>
    public static IServiceCollection AddSunCashSchedules(this IServiceCollection services)
    {
        services.AddHostedService<BackupOnShutdown>();
        foreach (var schedule in Schedules)
            services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(sp => new SyncScheduler(
                schedule, sp.GetRequiredService<SyncJobRunner>(), sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<SyncScheduler>>()));
        return services;
    }

    /// <summary>
    /// Le pianificazioni (docs/analisi.md, «Job di sincronizzazione»). GME: primo pomeriggio, con
    /// tentativi ogni 30 minuti finché escono i prezzi di domani. DA VERIFICARE: orario di
    /// pubblicazione degli esiti MGP. Energia: ogni ora al minuto 10; se il fornitore chiede di
    /// rallentare si aspetta il ciclo successivo, mai un tentativo ravvicinato. Backup: di notte.
    /// </summary>
    public static IReadOnlyList<SyncSchedule> Schedules { get; } =
    [
        new(GmePricesJob.JobName, "30 13 * * *", TimeSpan.FromMinutes(30)),
        new(EnergyImportJob.JobName, "10 * * * *", null),
        new(DatabaseBackupJob.JobName, "17 3 * * *", null),
    ];

    /// <summary>I fornitori di energia disponibili, per nome di configurazione.</summary>
    public static IReadOnlyList<string> EnergyProviders { get; } = [FusionSolarEnergySource.ProviderName];

    /// <summary>Registra il fornitore scelto in <c>Energy:Provider</c> come <see cref="IEnergySource"/>.
    /// Un fornitore nuovo si aggiunge qui e in <see cref="EnergyProviders"/>, nient'altro.</summary>
    private static void AddEnergyProvider(IServiceCollection services, IConfiguration configuration)
    {
        switch (configuration[$"{EnergyOptions.Section}:Provider"])
        {
            case FusionSolarEnergySource.ProviderName:
                services.AddOptions<FusionSolarOptions>().Bind(configuration.GetSection(FusionSolarOptions.Section))
                    .ValidateDataAnnotations().ValidateOnStart();
                services.AddSingleton<FusionSolarSession>();
                services.AddSingleton<EndpointThrottle>();
                services.AddHttpClient<FusionSolarClient>();
                services.AddScoped<FusionSolarStationLocator>();
                services.AddScoped<IEnergySource, FusionSolarEnergySource>();
                break;
            default:
                // La validazione di EnergyOptions ferma l'avvio con un messaggio chiaro.
                break;
        }
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
            services.AddSingleton(TimeProvider.System);
    }
}
