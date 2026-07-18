using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeaveRequestEnhancement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "LeaveRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "LeaveRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancelledByUserId",
                table: "LeaveRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsuranceCertNumber",
                table: "LeaveRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRetroactive",
                table: "LeaveRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MedicalCertificateUrl",
                table: "LeaveRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_CancelledByUserId",
                table: "LeaveRequests",
                column: "CancelledByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_Users_CancelledByUserId",
                table: "LeaveRequests",
                column: "CancelledByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Permission cho phép nhân viên tự hủy đơn phép của chính mình.
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                columnTypes: new[] { "int", "nvarchar(50)", "nvarchar(100)", "nvarchar(255)", "nvarchar(50)", "nvarchar(20)" },
                values: new object[,]
                {
                    { 234, "Cancel", "LeaveRequest.Cancel.Self", "Nhân viên tự hủy đơn nghỉ phép của mình.", "LeaveRequest", "Self" },
                });

            // Gán cho tất cả role đang có LeaveRequest.Create.Self (116) — mọi nhân viên đều được tạo/hủy.
            migrationBuilder.Sql("""
                INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
                SELECT rp.[RoleId], 234
                FROM [RolePermissions] rp
                WHERE rp.[PermissionId] = 116
                  AND NOT EXISTS (
                      SELECT 1 FROM [RolePermissions] existing
                      WHERE existing.[RoleId] = rp.[RoleId] AND existing.[PermissionId] = 234);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [RolePermissions] WHERE [PermissionId] = 234;");
            migrationBuilder.Sql("DELETE FROM [Permissions] WHERE [Id] = 234;");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_Users_CancelledByUserId",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_CancelledByUserId",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "InsuranceCertNumber",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "IsRetroactive",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "MedicalCertificateUrl",
                table: "LeaveRequests");
        }
    }
}
