using SunCash.Domain.Tariffs;
using SunCash.Domain.Valuation;
using SunCash.Infrastructure.Persistence;
using SunCash.Infrastructure.Valuation;

namespace SunCash.Api.Endpoints;

/// <summary>Le sole scritture dell'API: dati inseriti a mano (offerte, bollette, pagamenti GSE).</summary>
public static class ManualDataEndpoints
{
    /// <summary>Registra gli endpoint.</summary>
    public static void MapManualDataEndpoints(this IEndpointRouteBuilder app)
    {
        var tariffs = app.MapGroup("/api/tariffs").WithTags("Offerte");
        tariffs.MapGet("", async (ManualDataStore store, CancellationToken ct) =>
            Results.Ok((await store.TariffsAsync(ct)).Select(TariffDto.From)));
        tariffs.MapPut("/{validFrom}", async (DateOnly validFrom, TariffDto body, ManualDataStore store, CancellationToken ct) =>
        {
            var tariff = body with { ValidFrom = validFrom };
            var errors = tariff.Validate();
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);
            await store.SaveTariffAsync(tariff.ToDomain(), ct);
            return Results.Ok(tariff);
        });
        tariffs.MapDelete("/{validFrom}", async (DateOnly validFrom, ManualDataStore store, CancellationToken ct) =>
            await store.DeleteTariffAsync(validFrom, ct) ? Results.NoContent() : Results.NotFound());

        app.MapGet("/api/regulated-charges", async (ManualDataStore store, CancellationToken ct) =>
            Results.Ok(await store.RegulatedChargesAsync(ct))).WithTags("Offerte");

        var bills = app.MapGroup("/api/bills").WithTags("Bollette");
        bills.MapGet("", async (ManualDataStore store, ValuationReader reader, CancellationToken ct) =>
        {
            var list = new List<BillDto>();
            foreach (var b in await store.BillsAsync(ct))
            {
                var totals = ValuationTotals.Of((await reader.ForDaysAsync(b.PeriodFrom, b.PeriodTo, ct)).SelectMany(d => d.Rows));
                list.Add(new BillDto(b.PeriodFrom, b.PeriodTo, b.ImportKwh, b.AmountEur, b.Origin, b.Notes,
                    totals.ImportWh / 1000m, totals.ImportCostEur));
            }

            return Results.Ok(list);
        });
        bills.MapPut("/{periodFrom}/{periodTo}", async (DateOnly periodFrom, DateOnly periodTo, BillInput body, ManualDataStore store, CancellationToken ct) =>
        {
            if (periodTo < periodFrom || body.AmountEur < 0 || body.ImportKwh < 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["periodTo"] = ["Il periodo deve finire dopo l'inizio, e importo e kWh non possono essere negativi."],
                });
            await store.SaveBillAsync(new BillRow
            {
                PeriodFrom = periodFrom, PeriodTo = periodTo, ImportKwh = body.ImportKwh, AmountEur = body.AmountEur,
                Origin = DataOrigin.Manual, Notes = body.Notes,
            }, ct);
            return Results.NoContent();
        });
        bills.MapDelete("/{periodFrom}/{periodTo}", async (DateOnly periodFrom, DateOnly periodTo, ManualDataStore store, CancellationToken ct) =>
            await store.DeleteBillAsync(periodFrom, periodTo, ct) ? Results.NoContent() : Results.NotFound());

        var gse = app.MapGroup("/api/gse-payments").WithTags("Pagamenti GSE");
        gse.MapGet("", async (ManualDataStore store, CancellationToken ct) =>
            Results.Ok((await store.GsePaymentsAsync(ct)).Values.OrderBy(p => p.Month)));
        gse.MapPut("/{month}", async (string month, GsePaymentInput body, ManualDataStore store, CancellationToken ct) =>
        {
            if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", out _) || body.AmountEur < 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["month"] = ["Il mese si scrive yyyy-MM e l'importo non può essere negativo."],
                });
            await store.SaveGsePaymentAsync(month, body.AmountEur, body.Notes, ct);
            return Results.NoContent();
        });
    }
}
