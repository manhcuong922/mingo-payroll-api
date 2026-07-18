using System;
using MPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260704143000_AttendanceDropLegacyTimeColumns")]
    public partial class AttendanceDropLegacyTimeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CheckInTime", table: "AttendanceRecords");
            migrationBuilder.DropColumn(name: "CheckOutTime", table: "AttendanceRecords");
            migrationBuilder.DropColumn(name: "NewCheckInTime", table: "AttendanceAdjustments");
            migrationBuilder.DropColumn(name: "NewCheckOutTime", table: "AttendanceAdjustments");
            migrationBuilder.DropColumn(name: "OriginalCheckInTime", table: "AttendanceAdjustments");
            migrationBuilder.DropColumn(name: "OriginalCheckOutTime", table: "AttendanceAdjustments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "CheckInTime",
                table: "AttendanceRecords",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CheckOutTime",
                table: "AttendanceRecords",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "NewCheckInTime",
                table: "AttendanceAdjustments",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "NewCheckOutTime",
                table: "AttendanceAdjustments",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "OriginalCheckInTime",
                table: "AttendanceAdjustments",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "OriginalCheckOutTime",
                table: "AttendanceAdjustments",
                type: "time",
                nullable: true);
        }
    }
}
