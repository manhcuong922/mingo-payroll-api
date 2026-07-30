using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PayrollPeriodStaleFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStale",
                table: "PayrollPeriods",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "StaleMarkedAt",
                table: "PayrollPeriods",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StaleReason",
                table: "PayrollPeriods",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsStale",
                table: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "StaleMarkedAt",
                table: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "StaleReason",
                table: "PayrollPeriods");
        }
    }
}
