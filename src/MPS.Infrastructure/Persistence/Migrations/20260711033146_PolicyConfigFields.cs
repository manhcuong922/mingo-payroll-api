using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PolicyConfigFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowRetroactive",
                table: "LeaveTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoFallbackToUnpaid",
                table: "LeaveTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MinNoticeDaysForLong",
                table: "LeaveTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinNoticeDaysForMedium",
                table: "LeaveTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinNoticeDaysForShort",
                table: "LeaveTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LateEarlyBackdateDaysAllowed",
                table: "AttendanceSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LateEarlyMaxMinutesPerOccurrence",
                table: "AttendanceSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LateEarlyMaxOccurrencesPerMonth",
                table: "AttendanceSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WfhMaxDaysPerMonth",
                table: "AttendanceSettings",
                type: "decimal(5,1)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WfhMaxDaysPerRequest",
                table: "AttendanceSettings",
                type: "decimal(5,1)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WfhMinNoticeDays",
                table: "AttendanceSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WfhWorkPlanMinLength",
                table: "AttendanceSettings",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowRetroactive",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "AutoFallbackToUnpaid",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "MinNoticeDaysForLong",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "MinNoticeDaysForMedium",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "MinNoticeDaysForShort",
                table: "LeaveTypes");

            migrationBuilder.DropColumn(
                name: "LateEarlyBackdateDaysAllowed",
                table: "AttendanceSettings");

            migrationBuilder.DropColumn(
                name: "LateEarlyMaxMinutesPerOccurrence",
                table: "AttendanceSettings");

            migrationBuilder.DropColumn(
                name: "LateEarlyMaxOccurrencesPerMonth",
                table: "AttendanceSettings");

            migrationBuilder.DropColumn(
                name: "WfhMaxDaysPerMonth",
                table: "AttendanceSettings");

            migrationBuilder.DropColumn(
                name: "WfhMaxDaysPerRequest",
                table: "AttendanceSettings");

            migrationBuilder.DropColumn(
                name: "WfhMinNoticeDays",
                table: "AttendanceSettings");

            migrationBuilder.DropColumn(
                name: "WfhWorkPlanMinLength",
                table: "AttendanceSettings");
        }
    }
}
