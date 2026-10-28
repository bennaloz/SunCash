using SunCash.Domain.Energy;
using SunCash.Domain.Time;
using SunCash.Infrastructure.Persistence;
using SunCash.Infrastructure.Sync;
using SunCash.Infrastructure.Valuation;

namespace SunCash.Api.Endpoints;

/// <summary>Stato dei job e avvio manuale.</summary>
public static class SyncEndpoints
{
    /// <summary>Oltre questo tempo senza un successo, un job è segnalato in dashboard.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(24);

    private const int GapLookbackDays = 30;

    /// <summary>Registra gli endpoint.</summary>
    public static void MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sync").WithTags("Sincronizzazione");

        group.MapGet("/status", async (SyncStateStore states, SyncJobRunner runner, ValuationReader reader, ManualDataStore manual,
            IEnergySource source, TimeProvider clock, CancellationToken ct) =>
        {
            var now = clock.GetUtcNow();
            var stored = (await states.ListAsync(ct)).ToDictionary(s => s.Job);
            var jobs = runner.JobNames().Select(name => stored.TryGetValue(name, out var s)
                ? new SyncJobStatusDto(name, s.Source, s.LastSuccessUtc, s.Cursor, s.LastError,
                    s.LastSuccessUtc is null || now - s.LastSuccessUtc > StaleAfter)
                : new SyncJobStatusDto(name, null, null, null, null, true)).ToList();

            var yesterday = ItalianTime.LocalDate(now).AddDays(-1);
            var gaps = (await reader.ForDaysAsync(yesterday.AddDays(1 - GapLookbackDays), yesterday, ct))
                .Where(d => d.Totals.RowsWithoutExport > 0)
                .Select(d => d.Day)
                .ToList();

            return Results.Ok(new SyncStatusDto(source.Id, jobs, gaps, (await manual.RegulatedChargesAsync(ct)).Count == 0));
        });

        group.MapPost("/{job}/run", async (string job, SyncJobRunner runner, CancellationToken ct) =>
            await runner.RunAsync(job, ct) is { } outcome ? Results.Ok(outcome) : Results.NotFound());
    }
}
