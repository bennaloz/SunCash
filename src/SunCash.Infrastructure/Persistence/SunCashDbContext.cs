using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SunCash.Infrastructure.Persistence;

/// <summary>
/// Il database SQLite di SunCash: solo dati grezzi e inseriti a mano, mai euro calcolati
/// (docs/analisi.md, «Modello dati»).
/// </summary>
public sealed class SunCashDbContext(DbContextOptions<SunCashDbContext> options) : DbContext(options)
{
    /// <summary>Prezzi GME.</summary>
    public DbSet<MarketPriceRow> MarketPrices => Set<MarketPriceRow>();
    /// <summary>Misure dei fornitori di energia.</summary>
    public DbSet<EnergyReadingRow> EnergyReadings => Set<EnergyReadingRow>();
    /// <summary>Offerte luce.</summary>
    public DbSet<TariffRow> Tariffs => Set<TariffRow>();
    /// <summary>Parte regolata ARERA.</summary>
    public DbSet<RegulatedChargesRow> RegulatedCharges => Set<RegulatedChargesRow>();
    /// <summary>Bollette reali.</summary>
    public DbSet<BillRow> Bills => Set<BillRow>();
    /// <summary>Prezzi minimi garantiti.</summary>
    public DbSet<PriceFloorRow> PriceFloors => Set<PriceFloorRow>();
    /// <summary>Pagamenti GSE.</summary>
    public DbSet<GsePaymentRow> GsePayments => Set<GsePaymentRow>();
    /// <summary>Stato dei job.</summary>
    public DbSet<SyncStateRow> SyncState => Set<SyncStateRow>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Tempo come epoch UTC in secondi, importi come REAL, enum come testo: docs/analisi.md, «Modello dati».
        builder.Properties<DateTimeOffset>().HaveConversion<UnixSecondsConverter>();
        builder.Properties<decimal>().HaveConversion<double>();
        builder.Properties<Enum>().HaveConversion<string>();
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<MarketPriceRow>(e =>
        {
            e.ToTable("market_prices");
            e.HasKey(x => new { x.TsUtc, x.Resolution, x.Market, x.Zone });
        });
        b.Entity<EnergyReadingRow>(e =>
        {
            e.ToTable("energy_readings");
            e.HasKey(x => new { x.TsUtc, x.Resolution, x.Source });
        });
        b.Entity<TariffRow>(e =>
        {
            e.ToTable("tariffs");
            e.HasKey(x => x.ValidFrom);
        });
        b.Entity<RegulatedChargesRow>(e =>
        {
            e.ToTable("regulated_charges");
            e.HasKey(x => x.ValidFrom);
        });
        b.Entity<BillRow>(e =>
        {
            e.ToTable("bills");
            e.HasKey(x => new { x.PeriodFrom, x.PeriodTo });
        });
        b.Entity<PriceFloorRow>(e =>
        {
            e.ToTable("price_floors");
            e.HasKey(x => x.Year);
            e.Property(x => x.Year).ValueGeneratedNever();
        });
        b.Entity<GsePaymentRow>(e =>
        {
            e.ToTable("gse_payments");
            e.HasKey(x => x.Month);
        });
        b.Entity<SyncStateRow>(e =>
        {
            e.ToTable("sync_state");
            e.HasKey(x => x.Job);
        });

        // Nomi di colonna in snake_case, come nell'analisi.
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(SnakeCase(property.Name));
    }

    private static string SnakeCase(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    /// <summary>DateTimeOffset ↔ secondi dall'epoch UTC.</summary>
    private sealed class UnixSecondsConverter() : ValueConverter<DateTimeOffset, long>(
        v => v.ToUnixTimeSeconds(),
        v => DateTimeOffset.FromUnixTimeSeconds(v));
}
