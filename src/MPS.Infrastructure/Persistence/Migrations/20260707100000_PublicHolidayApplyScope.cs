using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260707100000_PublicHolidayApplyScope")]
    public partial class PublicHolidayApplyScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplyScope",
                table: "PublicHolidays",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "AllEmployees");

            migrationBuilder.AddColumn<string>(
                name: "TargetDepartmentIds",
                table: "PublicHolidays",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetEmployeeIds",
                table: "PublicHolidays",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TargetEmployeeIds", table: "PublicHolidays");
            migrationBuilder.DropColumn(name: "TargetDepartmentIds", table: "PublicHolidays");
            migrationBuilder.DropColumn(name: "ApplyScope", table: "PublicHolidays");
        }
    }
}
