using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnionDuesAndKpcdExemptFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EmployeeUnionDuesCapPercent",
                table: "SocialInsuranceRateVersions",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 10.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployeeUnionDuesPercent",
                table: "SocialInsuranceRateVersions",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 1.00m);

            migrationBuilder.AddColumn<bool>(
                name: "IsUnionMember",
                table: "EmployeeInformations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UnionFundExemptFlag",
                table: "Contracts",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployeeUnionDuesCapPercent",
                table: "SocialInsuranceRateVersions");

            migrationBuilder.DropColumn(
                name: "EmployeeUnionDuesPercent",
                table: "SocialInsuranceRateVersions");

            migrationBuilder.DropColumn(
                name: "IsUnionMember",
                table: "EmployeeInformations");

            migrationBuilder.DropColumn(
                name: "UnionFundExemptFlag",
                table: "Contracts");
        }
    }
}
