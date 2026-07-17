using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PayrollDropEmployerParty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollPeriods_ContractParties_EmployerPartyId",
                table: "PayrollPeriods");

            migrationBuilder.DropIndex(
                name: "IX_PayrollPeriods_EmployerPartyId_PeriodYear_PeriodMonth",
                table: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "EmployerPartyId",
                table: "PayrollPeriods");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_PeriodYear_PeriodMonth",
                table: "PayrollPeriods",
                columns: new[] { "PeriodYear", "PeriodMonth" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollPeriods_PeriodYear_PeriodMonth",
                table: "PayrollPeriods");

            migrationBuilder.AddColumn<int>(
                name: "EmployerPartyId",
                table: "PayrollPeriods",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_EmployerPartyId_PeriodYear_PeriodMonth",
                table: "PayrollPeriods",
                columns: new[] { "EmployerPartyId", "PeriodYear", "PeriodMonth" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollPeriods_ContractParties_EmployerPartyId",
                table: "PayrollPeriods",
                column: "EmployerPartyId",
                principalTable: "ContractParties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
