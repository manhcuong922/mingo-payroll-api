using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeaveRequestMedicalCertificate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupportingDocumentUrl",
                table: "LeaveRequests");

            migrationBuilder.RenameColumn(
                name: "MedicalCertificateUrl",
                table: "LeaveRequests",
                newName: "MedicalCertificateObjectKey");

            migrationBuilder.AlterColumn<string>(
                name: "MedicalCertificateObjectKey",
                table: "LeaveRequests",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MedicalCertificateObjectKey",
                table: "LeaveRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "MedicalCertificateObjectKey",
                table: "LeaveRequests",
                newName: "MedicalCertificateUrl");

            migrationBuilder.AddColumn<string>(
                name: "SupportingDocumentUrl",
                table: "LeaveRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }
    }
}
