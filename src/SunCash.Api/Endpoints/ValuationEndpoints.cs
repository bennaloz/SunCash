using Microsoft.Extensions.Options;
using SunCash.Domain.Time;
using SunCash.Domain.Valuation;
using SunCash.Infrastructure.Options;
using SunCash.Infrastructure.Persistence;
using SunCash.Infrastructure.Valuation;

namespace SunCash.Api.Endpoints;

/// <summary>Letture della valorizzazione. Le date sono giorni italiani <c>yyyy-MM-dd</c>.</summary>
public static class ValuationEndpoints
{
    /// <summary>Periodo massimo di una richiesta, in giorni.</summary>
    public const int MaxDays = 366;

    /// <summary>Registra gli endpoint.</summary>
    public static void MapValuationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/valuation").WithTags("Valorizzazione");

        group.MapGet("/day/{date}", async (DateOnly date, ValuationReader reader, CancellationToken ct) =>
        {
            var day = (await reader.ForDaysAsync(date, date, ct)).Single();
            return Results.Ok(new { Date = date, Totals = TotalsDto.From(day.Totals), Rows = day.Rows.Select(ValuationRowDto.From) });
        });

        group.MapGet("/hours", async (DateOnly from, DateOnly to, ValuationReader reader, CancellationToken ct) =>
            InvalidRange(from, to) ?? Results.Ok((await reader.ForDaysAsync(from, to, ct))
                .SelectMany(d => d.Rows)
                .GroupBy(r => ItalianTime.HourStartUtc(r.Interval.StartUtc))
                .Select(g => new PeriodTotalsDto($"{g.First().LocalDate:yyyy-MM-dd} {g.First().LocalLabel}", g.Key,
                    TotalsDto.From(ValuationTotals.Of(g))))));

        group.MapGet("/days", async (DateOnly from, DateOnly to, ValuationReader reader, CancellationToken ct) =>
            InvalidRange(from, to) ?? Results.Ok((await reader.ForDaysAsync(from, to, ct))
                .Select(d => new PeriodTotalsDto(d.Day.ToString("yyyy-MM-dd"), ItalianTime.DayBoundsUtc(d.Day).StartUtc,
                    TotalsDto.From(d.Totals)))));

        group.MapGet("/months", async (int year, ValuationReader reader, ManualDataStore manual, CancellationToken ct) =>
        {
            var days = await reader.ForDaysAsync(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), ct);
            var payments = await manual.GsePaymentsAsync(ct);
            return Results.Ok(days
                .GroupBy(d => d.Day.ToString("yyyy-MM"))
                .Select(g => new MonthTotalsDto(g.Key, TotalsDto.From(ValuationTotals.Of(g.SelectMany(d => d.Rows))),
                    payments.TryGetValue(g.Key, out var p) ? p.AmountEur : null)));
        });

        app.MapGet("/api/summary", async (ValuationReader reader, IOptions<ValuationOptions> options, TimeProvider clock, CancellationToken ct) =>
        {
            var today = ItalianTime.LocalDate(clock.GetUtcNow());
            var days = await reader.ForDaysAsync(new DateOnly(today.Year, 1, 1), today, ct);
            ValuationTotals Of(Func<ValuedDay, bool> filter) => ValuationTotals.Of(days.Where(filter).SelectMany(d => d.Rows));

            var best = days.Where(d => d.Totals.BenefitEur > 0).MaxBy(d => d.Totals.BenefitEur);
            return Results.Ok(new SummaryDto(
                TotalsDto.From(Of(d => d.Day == today)),
                TotalsDto.From(Of(d => d.Day == today.AddDays(-1))),
                TotalsDto.From(Of(d => d.Day.Month == today.Month)),
                TotalsDto.From(Of(_ => true)),
                best is null ? null : new PeriodTotalsDto(best.Day.ToString("yyyy-MM-dd"), null, TotalsDto.From(best.Totals)),
                options.Value.Regime,
                ValuationCalculator.ExportValueForYear(options.Value.Regime, days.SelectMany(d => d.Rows)),
                options.Value.AnnualFixedCostsEur));
        });
    }

    private static IResult? InvalidRange(DateOnly from, DateOnly to) =>
        to < from || to.DayNumber - from.DayNumber >= MaxDays
            ? Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["to"] = [$"Il periodo va da 1 a {MaxDays} giorni, con 'to' non prima di 'from'."],
            })
            : null;
}
