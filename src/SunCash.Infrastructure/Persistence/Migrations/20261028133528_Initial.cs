using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SunCash.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bills",
                columns: table => new
                {
                    period_from = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    period_to = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    import_kwh = table.Column<double>(type: "REAL", nullable: true),
                    amount_eur = table.Column<double>(type: "REAL", nullable: false),
                    origin = table.Column<string>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bills", x => new { x.period_from, x.period_to });
                });

            migrationBuilder.CreateTable(
                name: "energy_readings",
                columns: table => new
                {
                    ts_utc = table.Column<long>(type: "INTEGER", nullable: false),
                    resolution = table.Column<int>(type: "INTEGER", nullable: false),
                    source = table.Column<string>(type: "TEXT", nullable: false),
                    export_wh = table.Column<long>(type: "INTEGER", nullable: true),
                    import_wh = table.Column<long>(type: "INTEGER", nullable: true),
                    production_wh = table.Column<long>(type: "INTEGER", nullable: true),
                    consumption_wh = table.Column<long>(type: "INTEGER", nullable: true),
                    battery_charge_wh = table.Column<long>(type: "INTEGER", nullable: true),
                    battery_discharge_wh = table.Column<long>(type: "INTEGER", nullable: true),
                    imported_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_energy_readings", x => new { x.ts_utc, x.resolution, x.source });
                });

            migrationBuilder.CreateTable(
                name: "gse_payments",
                columns: table => new
                {
                    month = table.Column<string>(type: "TEXT", nullable: false),
                    amount_eur = table.Column<double>(type: "REAL", nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gse_payments", x => x.month);
                });

            migrationBuilder.CreateTable(
                name: "market_prices",
                columns: table => new
                {
                    ts_utc = table.Column<long>(type: "INTEGER", nullable: false),
                    resolution = table.Column<int>(type: "INTEGER", nullable: false),
                    market = table.Column<string>(type: "TEXT", nullable: false),
                    zone = table.Column<string>(type: "TEXT", nullable: false),
                    eur_mwh = table.Column<double>(type: "REAL", nullable: false),
                    imported_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_prices", x => new { x.ts_utc, x.resolution, x.market, x.zone });
                });

            migrationBuilder.CreateTable(
                name: "price_floors",
                columns: table => new
                {
                    year = table.Column<int>(type: "INTEGER", nullable: false),
                    eur_mwh = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_floors", x => x.year);
                });

            migrationBuilder.CreateTable(
                name: "regulated_charges",
                columns: table => new
                {
                    valid_from = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    network_eur_kwh = table.Column<double>(type: "REAL", nullable: false),
                    excise_eur_kwh = table.Column<double>(type: "REAL", nullable: false),
                    vat_rate = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulated_charges", x => x.valid_from);
                });

            migrationBuilder.CreateTable(
                name: "sync_state",
                columns: table => new
                {
                    job = table.Column<string>(type: "TEXT", nullable: false),
                    source = table.Column<string>(type: "TEXT", nullable: true),
                    last_success_utc = table.Column<long>(type: "INTEGER", nullable: true),
                    cursor = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    last_error = table.Column<string>(type: "TEXT", nullable: true),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_state", x => x.job);
                });

            migrationBuilder.CreateTable(
                name: "tariffs",
                columns: table => new
                {
                    valid_from = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    energy_f1_eur_kwh = table.Column<double>(type: "REAL", nullable: true),
                    energy_f2_eur_kwh = table.Column<double>(type: "REAL", nullable: true),
                    energy_f3_eur_kwh = table.Column<double>(type: "REAL", nullable: true),
                    pun_spread_eur_kwh = table.Column<double>(type: "REAL", nullable: true),
                    pun_basis = table.Column<string>(type: "TEXT", nullable: true),
                    losses_factor = table.Column<double>(type: "REAL", nullable: false),
                    origin = table.Column<string>(type: "TEXT", nullable: false),
                    offer_code = table.Column<string>(type: "TEXT", nullable: true),
                    notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tariffs", x => x.valid_from);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bills");

            migrationBuilder.DropTable(
                name: "energy_readings");

            migrationBuilder.DropTable(
                name: "gse_payments");

            migrationBuilder.DropTable(
                name: "market_prices");

            migrationBuilder.DropTable(
                name: "price_floors");

            migrationBuilder.DropTable(
                name: "regulated_charges");

            migrationBuilder.DropTable(
                name: "sync_state");

            migrationBuilder.DropTable(
                name: "tariffs");
        }
    }
}
