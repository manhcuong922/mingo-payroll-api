using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Compliance_FundEntryEligibilityShortTermPitAppliedRegion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EligibilityMode",
                table: "SocialInsuranceRateVersions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EligibleContractSubtypesCsv",
                table: "SocialInsuranceRateVersions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HealthCapMultiplier",
                table: "SocialInsuranceRateVersions",
                type: "int",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<int>(
                name: "MinTenureMonths",
                table: "SocialInsuranceRateVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShortTermContractMaxMonths",
                table: "PitRegulationVersions",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<decimal>(
                name: "ShortTermFlatRatePercent",
                table: "PitRegulationVersions",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 10.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShortTermTaxExemptMonthlyIncome",
                table: "PitRegulationVersions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 2000000m);

            migrationBuilder.AddColumn<string>(
                name: "AppliedRegionCode",
                table: "MinimumWageVersions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SocialInsuranceFundEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SocialInsuranceRateVersionId = table.Column<int>(type: "int", nullable: false),
                    FundCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FundName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmployeePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EmployerPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialInsuranceFundEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialInsuranceFundEntries_SocialInsuranceRateVersions_SocialInsuranceRateVersionId",
                        column: x => x.SocialInsuranceRateVersionId,
                        principalTable: "SocialInsuranceRateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SocialInsuranceFundEntries_SocialInsuranceRateVersionId_FundCode",
                table: "SocialInsuranceFundEntries",
                columns: new[] { "SocialInsuranceRateVersionId", "FundCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SocialInsuranceFundEntries");

            migrationBuilder.DropColumn(
                name: "EligibilityMode",
                table: "SocialInsuranceRateVersions");

            migrationBuilder.DropColumn(
                name: "EligibleContractSubtypesCsv",
                table: "SocialInsuranceRateVersions");

            migrationBuilder.DropColumn(
                name: "HealthCapMultiplier",
                table: "SocialInsuranceRateVersions");

            migrationBuilder.DropColumn(
                name: "MinTenureMonths",
                table: "SocialInsuranceRateVersions");

            migrationBuilder.DropColumn(
                name: "ShortTermContractMaxMonths",
                table: "PitRegulationVersions");

            migrationBuilder.DropColumn(
                name: "ShortTermFlatRatePercent",
                table: "PitRegulationVersions");

            migrationBuilder.DropColumn(
                name: "ShortTermTaxExemptMonthlyIncome",
                table: "PitRegulationVersions");

            migrationBuilder.DropColumn(
                name: "AppliedRegionCode",
                table: "MinimumWageVersions");
        }
    }
}
