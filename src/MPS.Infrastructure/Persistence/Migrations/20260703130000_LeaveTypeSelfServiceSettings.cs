using MPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260703130000_LeaveTypeSelfServiceSettings")]
    public partial class LeaveTypeSelfServiceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowEmployeeSelection",
                table: "LeaveTypes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxDaysPerYear",
                table: "LeaveTypes",
                type: "decimal(5,1)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxDaysPerMonth",
                table: "LeaveTypes",
                type: "decimal(5,1)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxDaysPerWeek",
                table: "LeaveTypes",
                type: "decimal(5,1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplyScope",
                table: "LeaveTypes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "AllEmployees");

            migrationBuilder.AddColumn<string>(
                name: "TargetDepartmentIds",
                table: "LeaveTypes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetEmployeeIds",
                table: "LeaveTypes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AllowEmployeeSelection", table: "LeaveTypes");
            migrationBuilder.DropColumn(name: "MaxDaysPerYear", table: "LeaveTypes");
            migrationBuilder.DropColumn(name: "MaxDaysPerMonth", table: "LeaveTypes");
            migrationBuilder.DropColumn(name: "MaxDaysPerWeek", table: "LeaveTypes");
            migrationBuilder.DropColumn(name: "ApplyScope", table: "LeaveTypes");
            migrationBuilder.DropColumn(name: "TargetDepartmentIds", table: "LeaveTypes");
            migrationBuilder.DropColumn(name: "TargetEmployeeIds", table: "LeaveTypes");
        }
    }
}
