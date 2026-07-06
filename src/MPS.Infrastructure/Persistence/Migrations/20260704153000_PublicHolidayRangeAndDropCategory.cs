using System;
using MPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260704153000_PublicHolidayRangeAndDropCategory")]
    public partial class PublicHolidayRangeAndDropCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PublicHolidays_HolidayDate",
                table: "PublicHolidays");

            migrationBuilder.AddColumn<DateOnly>(
                name: "HolidayEndDate",
                table: "PublicHolidays",
                type: "date",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [PublicHolidays]
                SET [HolidayEndDate] = [HolidayDate]
                WHERE [HolidayEndDate] IS NULL;
                """);

            migrationBuilder.Sql("""
                ;WITH Ordered AS (
                    SELECT
                        [Id],
                        [Name],
                        [Description],
                        [HolidayDate],
                        DATEADD(day, -ROW_NUMBER() OVER (
                            PARTITION BY [Name], [Description]
                            ORDER BY [HolidayDate], [Id]
                        ), CAST([HolidayDate] AS date)) AS [IslandKey]
                    FROM [PublicHolidays]
                    WHERE [IsActive] = CAST(1 AS bit)
                ),
                Grouped AS (
                    SELECT
                        [Id],
                        FIRST_VALUE([Id]) OVER (
                            PARTITION BY [Name], [Description], [IslandKey]
                            ORDER BY [HolidayDate], [Id]
                        ) AS [KeepId],
                        MIN([HolidayDate]) OVER (
                            PARTITION BY [Name], [Description], [IslandKey]
                        ) AS [StartDate],
                        MAX([HolidayDate]) OVER (
                            PARTITION BY [Name], [Description], [IslandKey]
                        ) AS [EndDate],
                        COUNT(*) OVER (
                            PARTITION BY [Name], [Description], [IslandKey]
                        ) AS [GroupCount]
                    FROM Ordered
                )
                UPDATE h
                SET
                    [HolidayDate] = g.[StartDate],
                    [HolidayEndDate] = g.[EndDate]
                FROM [PublicHolidays] h
                INNER JOIN Grouped g ON h.[Id] = g.[Id]
                WHERE g.[Id] = g.[KeepId] AND g.[GroupCount] > 1;
                """);

            migrationBuilder.Sql("""
                ;WITH Ordered AS (
                    SELECT
                        [Id],
                        [Name],
                        [Description],
                        [HolidayDate],
                        DATEADD(day, -ROW_NUMBER() OVER (
                            PARTITION BY [Name], [Description]
                            ORDER BY [HolidayDate], [Id]
                        ), CAST([HolidayDate] AS date)) AS [IslandKey]
                    FROM [PublicHolidays]
                    WHERE [IsActive] = CAST(1 AS bit)
                ),
                Grouped AS (
                    SELECT
                        [Id],
                        FIRST_VALUE([Id]) OVER (
                            PARTITION BY [Name], [Description], [IslandKey]
                            ORDER BY [HolidayDate], [Id]
                        ) AS [KeepId],
                        COUNT(*) OVER (
                            PARTITION BY [Name], [Description], [IslandKey]
                        ) AS [GroupCount]
                    FROM Ordered
                )
                DELETE h
                FROM [PublicHolidays] h
                INNER JOIN Grouped g ON h.[Id] = g.[Id]
                WHERE g.[Id] <> g.[KeepId] AND g.[GroupCount] > 1;
                """);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "HolidayEndDate",
                table: "PublicHolidays",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Category",
                table: "PublicHolidays");

            migrationBuilder.DropColumn(
                name: "PayRateOverride",
                table: "PublicHolidays");

            migrationBuilder.CreateIndex(
                name: "IX_PublicHolidays_HolidayDate_HolidayEndDate",
                table: "PublicHolidays",
                columns: new[] { "HolidayDate", "HolidayEndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PublicHolidays_HolidayDate_HolidayEndDate",
                table: "PublicHolidays");

            migrationBuilder.AddColumn<byte>(
                name: "Category",
                table: "PublicHolidays",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<decimal>(
                name: "PayRateOverride",
                table: "PublicHolidays",
                type: "decimal(4,2)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "HolidayEndDate",
                table: "PublicHolidays");

            migrationBuilder.CreateIndex(
                name: "IX_PublicHolidays_HolidayDate",
                table: "PublicHolidays",
                column: "HolidayDate",
                unique: true,
                filter: "[IsActive] = 1");
        }
    }
}
