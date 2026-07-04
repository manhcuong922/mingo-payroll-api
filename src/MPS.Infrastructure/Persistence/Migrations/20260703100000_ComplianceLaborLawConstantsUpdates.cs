using System;
using MPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260703100000_ComplianceLaborLawConstantsUpdates")]
    public partial class ComplianceLaborLawConstantsUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NightShiftSurchargePercent",
                table: "LaborLawConstantsVersions",
                newName: "OvertimeNightExtraPercent");

            migrationBuilder.AddColumn<decimal>(
                name: "MaxRegularHoursPerWeek",
                table: "LaborLawConstantsVersions",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 48.00m);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "NightStartTime",
                table: "LaborLawConstantsVersions",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(22, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "NightEndTime",
                table: "LaborLawConstantsVersions",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(6, 0, 0));

            migrationBuilder.AddColumn<decimal>(
                name: "NightWorkSurchargePercent",
                table: "LaborLawConstantsVersions",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 30.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "OvertimeMaxHoursPerMonth",
                table: "LaborLawConstantsVersions",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 40.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "OvertimeNormalUpliftPercent",
                table: "LaborLawConstantsVersions",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 50.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "WorkRateNormalFloor",
                table: "LaborLawConstantsVersions",
                type: "decimal(4,2)",
                nullable: false,
                defaultValue: 1.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "WorkRateWeeklyRestFloor",
                table: "LaborLawConstantsVersions",
                type: "decimal(4,2)",
                nullable: false,
                defaultValue: 2.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "WorkRateHolidayFloor",
                table: "LaborLawConstantsVersions",
                type: "decimal(4,2)",
                nullable: false,
                defaultValue: 3.00m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MaxRegularHoursPerWeek", table: "LaborLawConstantsVersions");
            migrationBuilder.DropColumn(name: "NightStartTime", table: "LaborLawConstantsVersions");
            migrationBuilder.DropColumn(name: "NightEndTime", table: "LaborLawConstantsVersions");
            migrationBuilder.DropColumn(name: "NightWorkSurchargePercent", table: "LaborLawConstantsVersions");
            migrationBuilder.DropColumn(name: "OvertimeMaxHoursPerMonth", table: "LaborLawConstantsVersions");
            migrationBuilder.DropColumn(name: "OvertimeNormalUpliftPercent", table: "LaborLawConstantsVersions");
            migrationBuilder.DropColumn(name: "WorkRateNormalFloor", table: "LaborLawConstantsVersions");
            migrationBuilder.DropColumn(name: "WorkRateWeeklyRestFloor", table: "LaborLawConstantsVersions");
            migrationBuilder.DropColumn(name: "WorkRateHolidayFloor", table: "LaborLawConstantsVersions");

            migrationBuilder.RenameColumn(
                name: "OvertimeNightExtraPercent",
                table: "LaborLawConstantsVersions",
                newName: "NightShiftSurchargePercent");
        }
    }
}
