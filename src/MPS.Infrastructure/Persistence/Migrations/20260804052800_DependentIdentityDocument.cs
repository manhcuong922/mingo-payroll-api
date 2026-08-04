using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DependentIdentityDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupportingDocumentUrl",
                table: "DependentRegistrationRequests");

            migrationBuilder.AddColumn<DateOnly>(
                name: "IdentityDocumentIssuedDate",
                table: "EmployeeDependents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentityDocumentNumber",
                table: "EmployeeDependents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "IdentityDocumentType",
                table: "EmployeeDependents",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "IdentityDocumentIssuedDate",
                table: "DependentRegistrationRequests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentityDocumentNumber",
                table: "DependentRegistrationRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "IdentityDocumentType",
                table: "DependentRegistrationRequests",
                type: "tinyint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdentityDocumentIssuedDate",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "IdentityDocumentNumber",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "IdentityDocumentType",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "IdentityDocumentIssuedDate",
                table: "DependentRegistrationRequests");

            migrationBuilder.DropColumn(
                name: "IdentityDocumentNumber",
                table: "DependentRegistrationRequests");

            migrationBuilder.DropColumn(
                name: "IdentityDocumentType",
                table: "DependentRegistrationRequests");

            migrationBuilder.AddColumn<string>(
                name: "SupportingDocumentUrl",
                table: "DependentRegistrationRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }
    }
}
