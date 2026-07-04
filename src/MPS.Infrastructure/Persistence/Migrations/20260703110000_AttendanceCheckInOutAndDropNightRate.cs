using System;
using MPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260703110000_AttendanceCheckInOutAndDropNightRate")]
    public partial class AttendanceCheckInOutAndDropNightRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkRateNight",
                table: "AttendanceRules");

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckInAt",
                table: "AttendanceRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckOutAt",
                table: "AttendanceRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NewCheckInAt",
                table: "AttendanceAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NewCheckOutAt",
                table: "AttendanceAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OriginalCheckInAt",
                table: "AttendanceAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OriginalCheckOutAt",
                table: "AttendanceAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [AttendanceRecords]
                SET
                    [CheckInAt] = CASE
                        WHEN [CheckInTime] IS NULL THEN NULL
                        ELSE DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00:00' AS time), [CheckInTime]), CAST([WorkDate] AS datetime2))
                    END,
                    [CheckOutAt] = CASE
                        WHEN [CheckOutTime] IS NULL THEN NULL
                        ELSE DATEADD(
                            DAY,
                            CASE WHEN [CheckInTime] IS NOT NULL AND [CheckOutTime] <= [CheckInTime] THEN 1 ELSE 0 END,
                            DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00:00' AS time), [CheckOutTime]), CAST([WorkDate] AS datetime2)))
                    END
                WHERE [CheckInAt] IS NULL OR [CheckOutAt] IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE a
                SET
                    [NewCheckInAt] = CASE
                        WHEN a.[NewCheckInTime] IS NULL THEN NULL
                        ELSE DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00:00' AS time), a.[NewCheckInTime]), CAST(r.[WorkDate] AS datetime2))
                    END,
                    [NewCheckOutAt] = CASE
                        WHEN a.[NewCheckOutTime] IS NULL THEN NULL
                        ELSE DATEADD(
                            DAY,
                            CASE WHEN a.[NewCheckInTime] IS NOT NULL AND a.[NewCheckOutTime] <= a.[NewCheckInTime] THEN 1 ELSE 0 END,
                            DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00:00' AS time), a.[NewCheckOutTime]), CAST(r.[WorkDate] AS datetime2)))
                    END,
                    [OriginalCheckInAt] = CASE
                        WHEN a.[OriginalCheckInTime] IS NULL THEN NULL
                        ELSE DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00:00' AS time), a.[OriginalCheckInTime]), CAST(r.[WorkDate] AS datetime2))
                    END,
                    [OriginalCheckOutAt] = CASE
                        WHEN a.[OriginalCheckOutTime] IS NULL THEN NULL
                        ELSE DATEADD(
                            DAY,
                            CASE WHEN a.[OriginalCheckInTime] IS NOT NULL AND a.[OriginalCheckOutTime] <= a.[OriginalCheckInTime] THEN 1 ELSE 0 END,
                            DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00:00' AS time), a.[OriginalCheckOutTime]), CAST(r.[WorkDate] AS datetime2)))
                    END
                FROM [AttendanceAdjustments] a
                INNER JOIN [AttendanceRecords] r ON r.[Id] = a.[AttendanceRecordId]
                WHERE a.[NewCheckInAt] IS NULL
                   OR a.[NewCheckOutAt] IS NULL
                   OR a.[OriginalCheckInAt] IS NULL
                   OR a.[OriginalCheckOutAt] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CheckInAt", table: "AttendanceRecords");
            migrationBuilder.DropColumn(name: "CheckOutAt", table: "AttendanceRecords");
            migrationBuilder.DropColumn(name: "NewCheckInAt", table: "AttendanceAdjustments");
            migrationBuilder.DropColumn(name: "NewCheckOutAt", table: "AttendanceAdjustments");
            migrationBuilder.DropColumn(name: "OriginalCheckInAt", table: "AttendanceAdjustments");
            migrationBuilder.DropColumn(name: "OriginalCheckOutAt", table: "AttendanceAdjustments");

            migrationBuilder.AddColumn<decimal>(
                name: "WorkRateNight",
                table: "AttendanceRules",
                type: "decimal(4,2)",
                nullable: false,
                defaultValue: 1.30m);
        }
    }
}
