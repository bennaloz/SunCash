using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SunCash.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportedSelfConsumption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "self_consumption_wh",
                table: "energy_readings",
                type: "INTEGER",
                nullable: true);

            // I giorni già importati non hanno l'autoconsumo dichiarato e hanno la produzione letta da
            // un altro campo: si reimportano da Sync:StartDate (gli upsert li sovrascrivono).
            migrationBuilder.Sql("UPDATE sync_state SET cursor = NULL WHERE job = 'energy-import';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "self_consumption_wh",
                table: "energy_readings");
        }
    }
}
