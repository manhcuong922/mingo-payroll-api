using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceGracePeriodSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EarlyLeaveGraceMinutes",
                table: "AttendanceSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HalfDayThresholdMinutes",
                table: "AttendanceSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LateGraceMinutes",
                table: "AttendanceSettings",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EarlyLeaveGraceMinutes",
                table: "AttendanceSettings");

            migrationBuilder.DropColumn(
                name: "HalfDayThresholdMinutes",
                table: "AttendanceSettings");

            migrationBuilder.DropColumn(
                name: "LateGraceMinutes",
                table: "AttendanceSettings");
        }
    }
}
