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

    private static readonly DateOnly Start = new(2026, 10, 24);

    [Fact]
    public async Task Con_un_giorno_per_esecuzione_legge_ieri_oggi_poi_lo_storico_dal_piu_recente()
    {
        var source = new FakeSource { MaxDaysPerRun = 1 };
        await using var db = await TestDatabase.CreateAsync();

        // Sette esecuzioni orarie, dalle 12 alle 18.
        for (var run = 0; run < 7; run++)
        {
            Assert.Equal(SyncStatus.Done, (await RunAsync(db, source)).Status);
            _clock.Advance(TimeSpan.FromHours(1));
        }

        // Ieri; oggi; storico 26 e 25; alle 16 oggi è vecchio di tre ore e passa davanti; poi il 24;
        // a storico completo resta solo oggi.
        Assert.Equal(["10-27", "10-28", "10-26", "10-25", "10-28", "10-24", "10-28"], source.Requested.Select(d => d.ToString("MM-dd")));
        await using var context = db.NewContext();
        var state = await context.SyncState.SingleAsync();
        Assert.Equal(Start, state.Cursor);
        Assert.Equal("finto", state.Source);
    }

    [Fact]
    public async Task Senza_limite_del_fornitore_fa_tutto_in_una_volta()
    {
        var source = new FakeSource();
        await using var db = await TestDatabase.CreateAsync();

        var outcome = await RunAsync(db, source);

        Assert.Equal(SyncStatus.Done, outcome.Status);
        Assert.Equal(["10-27", "10-28", "10-26", "10-25", "10-24"], source.Requested.Select(d => d.ToString("MM-dd")));
        await using var context = db.NewContext();
        Assert.Equal(Start, (await context.SyncState.SingleAsync()).Cursor);
    }

    [Fact]
    public async Task Ieri_letto_subito_dopo_mezzanotte_si_rilegge_quando_il_cloud_ha_consolidato()
    {
        var source = new FakeSource { MaxDaysPerRun = 1 };
        await using var db = await TestDatabase.CreateAsync();
        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 28, 23, 30, 0, TimeSpan.Zero)); // 29 ottobre, 00:30 italiane

        await RunAsync(db, source);                     // 00:30: ieri (28), letto troppo presto
        _clock.Advance(TimeSpan.FromHours(1));
        await RunAsync(db, source);                     // 01:30: di nuovo il 28
        _clock.Advance(TimeSpan.FromHours(1));
        await RunAsync(db, source);                     // 02:30: il 28 è a posto, tocca a oggi

        Assert.Equal(["10-28", "10-28", "10-29"], source.Requested.Select(d => d.ToString("MM-dd")));
    }

    [Fact]
    public async Task Se_il_fornitore_chiede_di_rallentare_si_ferma_e_tiene_quello_che_ha_letto()
    {
        var source = new FakeSource { TooFrequentFrom = 3 };
        await using var db = await TestDatabase.CreateAsync();

        var outcome = await RunAsync(db, source);

        Assert.Equal(SyncStatus.RetryLater, outcome.Status);
        Assert.Equal(3, source.Requested.Count);
        await using var context = db.NewContext();
        Assert.Equal(2, await context.EnergyReadings.CountAsync());
    }

    private async Task<SyncOutcome> RunAsync(TestDatabase db, IEnergySource source)
    {
        await using var context = db.NewContext();
        var job = new EnergyImportJob(source, new EnergyReadingStore(context, _clock), new SyncStateStore(context, _clock),
            MsOptions.Create(new SyncOptions { StartDate = Start }),
            MsOptions.Create(new EnergyOptions { Provider = "finto", MaxBackfillDaysPerRun = 10 }),
            _clock);
        return await job.RunAsync(CancellationToken.None);
    }

    private sealed class FakeSource : IEnergySource
    {
        public List<DateOnly> Requested { get; } = [];

        public int? TooFrequentFrom { get; init; }

        public int? MaxDaysPerRun { get; init; }

        public string Id => "finto";

        public EnergySourceCapabilities Capabilities => new([60], true, MaxDaysPerRun);

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
