// Phase A — Night handling (Điều 98.2 + 106 BLLĐ).
//
// 1. AttendanceRules: + WorkRateNight (decimal(4,2), default 1.30)
// 2. AttendanceRecords: + DayHours, NightHours (decimal(5,2), default 0)
//    Backfill: tính lại breakdown bằng splitter qua DatabaseSeeder / chấp nhận 0
//    cho record cũ (record sẽ recompute khi update lần tới).

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Attendance_NightSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "WorkRateNight",
                table: "AttendanceRules",
                type: "decimal(4,2)",
                nullable: false,
                defaultValue: 1.30m);

            migrationBuilder.AddColumn<decimal>(
                name: "DayHours",
                table: "AttendanceRecords",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NightHours",
                table: "AttendanceRecords",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "NightHours",    table: "AttendanceRecords");
            migrationBuilder.DropColumn(name: "DayHours",      table: "AttendanceRecords");
            migrationBuilder.DropColumn(name: "WorkRateNight", table: "AttendanceRules");
        }
    }
}
