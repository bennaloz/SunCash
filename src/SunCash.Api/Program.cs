using System.Text.Json.Serialization;
using SunCash.Api.Endpoints;
using SunCash.Infrastructure;
using SunCash.Infrastructure.DbBackup;
using SunCash.Infrastructure.Options;
using SunCash.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSunCashInfrastructure(builder.Configuration);
if (builder.Configuration.GetValue($"{SyncOptions.Section}:Enabled", true))
    builder.Services.AddSunCashSchedules();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

var connectionString = DatabaseLocation.ConnectionString(builder.Configuration);
DatabaseLocation.EnsureDirectory(connectionString);
DatabaseRestore.RestoreIfMissing(connectionString, DatabaseLocation.BackupDirectory(builder.Configuration), app.Logger);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SunCashDbContext>();
    await DatabaseInitializer.InitializeAsync(db, CancellationToken.None);
    await RegulatedChargesSeed.ApplyAsync(scope.ServiceProvider.GetRequiredService<ManualDataStore>(), CancellationToken.None);
}

app.UseExceptionHandler();
app.MapOpenApi();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapValuationEndpoints();
app.MapManualDataEndpoints();
app.MapSyncEndpoints();

// Il frontend Angular, quando è pubblicato in wwwroot (App Service): ogni rotta che non è /api
// torna a index.html, così i link diretti (/calendario, /giorno/…) funzionano.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("{*path:nonfile:regex(^(?!api/).*$)}", "index.html");

app.Run();

/// <summary>Punto d'ingresso, visibile ai test di integrazione.</summary>
public partial class Program;
