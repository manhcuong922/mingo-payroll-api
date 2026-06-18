using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ComplianceAutoUpdate_ApprovePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Permission duyệt/từ chối proposal (ID 94) — dùng cho cả approve & reject.
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[] { 94, "Approve", "ComplianceAutoUpdate.Approve.All", "Duyệt/từ chối đề xuất cập nhật pháp luật tự động.", "ComplianceAutoUpdate", "All" });

            // Cấp cho SystemAdmin(1) + HR(2).
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 94, 1 }, { 94, 2 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 94, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 94, 2 });
            migrationBuilder.DeleteData(table: "Permissions", keyColumn: "Id", keyValue: 94);
        }
    }
}
