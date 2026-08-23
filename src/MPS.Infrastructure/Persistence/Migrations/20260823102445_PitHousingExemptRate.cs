using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PitHousingExemptRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HousingExemptRatePercent",
                table: "PitRegulationVersions",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 15.00m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HousingExemptRatePercent",
                table: "PitRegulationVersions");
        }
    }
}
