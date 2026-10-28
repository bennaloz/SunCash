using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SunCash.Infrastructure.Persistence;

/// <summary>Usata solo da <c>dotnet ef</c> per generare le migrazioni.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SunCashDbContext>
{
    public SunCashDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SunCashDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
