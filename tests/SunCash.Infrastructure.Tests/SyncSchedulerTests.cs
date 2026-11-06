using Cronos;
using SunCash.Infrastructure.Sync;

namespace SunCash.Infrastructure.Tests;

public class SyncSchedulerTests
{
    private static readonly CronExpression EveryHourAt10 = CronExpression.Parse("10 * * * *");

    // 6 novembre 2026, ora solare: 18:10 italiane = 17:10 UTC.
    private static readonly DateTimeOffset At1810 = new(2026, 11, 6, 17, 10, 0, TimeSpan.Zero);

    [Fact]
    public void Se_il_timer_si_sveglia_un_attimo_prima_non_rifa_la_stessa_ora()
    {
        var early = At1810.AddMilliseconds(-5);

        var next = SyncScheduler.NextOccurrence(EveryHourAt10, early, waitedFor: At1810);

        Assert.Equal(At1810.AddHours(1), next);
    }

    [Fact]
    public void Al_primo_giro_parte_dall_ora_successiva_ad_adesso()
    {
        var next = SyncScheduler.NextOccurrence(EveryHourAt10, At1810.AddMinutes(-20), waitedFor: null);

        Assert.Equal(At1810, next);
    }
}
