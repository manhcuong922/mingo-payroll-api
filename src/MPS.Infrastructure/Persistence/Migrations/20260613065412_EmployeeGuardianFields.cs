using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeGuardianFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuardianAddress",
                table: "EmployeeInformations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianFullName",
                table: "EmployeeInformations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianPhoneNo",
                table: "EmployeeInformations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianRelationship",
                table: "EmployeeInformations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuardianAddress",
                table: "EmployeeInformations");

            migrationBuilder.DropColumn(
                name: "GuardianFullName",
                table: "EmployeeInformations");

            migrationBuilder.DropColumn(
                name: "GuardianPhoneNo",
                table: "EmployeeInformations");

            migrationBuilder.DropColumn(
                name: "GuardianRelationship",
                table: "EmployeeInformations");
        }
    }
}
