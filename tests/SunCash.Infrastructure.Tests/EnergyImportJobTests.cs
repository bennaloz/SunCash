using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using SunCash.Domain.Energy;
using SunCash.Domain.Time;
using SunCash.Infrastructure.Energy;
using SunCash.Infrastructure.Options;
using SunCash.Infrastructure.Persistence;
using SunCash.Infrastructure.Sync;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace SunCash.Infrastructure.Tests;

/// <summary>Il job parla solo con il contratto: un fornitore finto basta a provarlo.</summary>
public class EnergyImportJobTests
{
    // 28 ottobre 2026, 12 italiane.
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 28, 11, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Importa_ieri_e_oggi_poi_ricostruisce_lo_storico_e_sposta_il_cursore()
    {
        var source = new FakeSource();
        await using var db = await TestDatabase.CreateAsync();

        var outcome = await RunAsync(db, source, startDate: new DateOnly(2026, 10, 24), maxBackfill: 10);

        Assert.Equal(SyncStatus.Done, outcome.Status);
        Assert.Equal(["2026-10-27", "2026-10-28", "2026-10-24", "2026-10-25", "2026-10-26"], source.Requested.Select(d => d.ToString("yyyy-MM-dd")));
        await using var context = db.NewContext();
        Assert.Equal(5, await context.EnergyReadings.CountAsync());
        var state = await context.SyncState.SingleAsync();
        Assert.Equal(new DateOnly(2026, 10, 27), state.Cursor);
        Assert.Equal("finto", state.Source);
    }

    [Fact]
    public async Task Il_backfill_si_ferma_al_massimo_di_giorni_per_esecuzione()
    {
        var source = new FakeSource();
        await using var db = await TestDatabase.CreateAsync();

        var outcome = await RunAsync(db, source, startDate: new DateOnly(2026, 10, 1), maxBackfill: 3);

        Assert.Equal(SyncStatus.RetryLater, outcome.Status);
        Assert.Equal(5, source.Requested.Count); // ieri, oggi e tre giorni di storico
    }

    [Fact]
    public async Task Se_il_fornitore_chiede_di_rallentare_ci_si_ferma_subito()
    {
        var source = new FakeSource { TooFrequentFrom = 2 };
        await using var db = await TestDatabase.CreateAsync();

        var outcome = await RunAsync(db, source, startDate: new DateOnly(2026, 10, 1), maxBackfill: 10);

        Assert.Equal(SyncStatus.RetryLater, outcome.Status);
        Assert.Equal(2, source.Requested.Count);
    }

    private async Task<SyncOutcome> RunAsync(TestDatabase db, IEnergySource source, DateOnly startDate, int maxBackfill)
    {
        await using var context = db.NewContext();
        var job = new EnergyImportJob(source, new EnergyReadingStore(context, _clock), new SyncStateStore(context, _clock),
            MsOptions.Create(new SyncOptions { StartDate = startDate }),
            MsOptions.Create(new EnergyOptions { Provider = "finto", MaxBackfillDaysPerRun = maxBackfill }),
            _clock);
        return await job.RunAsync(CancellationToken.None);
    }

    private sealed class FakeSource : IEnergySource
    {
        public List<DateOnly> Requested { get; } = [];

        public int? TooFrequentFrom { get; init; }

        public string Id => "finto";

        public EnergySourceCapabilities Capabilities { get; } = new([60], true);

        public Task<EnergyFetchResult> FetchDayAsync(DateOnly localDate, CancellationToken cancellationToken)
        {
            Requested.Add(localDate);
            if (Requested.Count >= TooFrequentFrom)
                return Task.FromResult(EnergyFetchResult.RetryNextCycle("piano"));
            var (start, _) = ItalianTime.DayBoundsUtc(localDate);
            return Task.FromResult(EnergyFetchResult.Done([new EnergyReading(new Interval(start.AddHours(12), 60), Id, 1000, 0, 2000, 1500, 0, 0)]));
        }
    }
}
