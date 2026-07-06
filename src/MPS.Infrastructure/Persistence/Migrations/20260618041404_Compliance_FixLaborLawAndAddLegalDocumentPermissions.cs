using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Compliance_FixLaborLawAndAddLegalDocumentPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // LegalDocument.Read.All đã tồn tại trong DB với Id=89 (seed trước migration này).
            // Chỉ cần insert Write.All (Id=91) và gán RolePermissions cho cả Read(89) + Write(91).
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[,]
                {
                    { 91, "Write", "LegalDocument.Write.All", "Tạo/cập nhật/liên kết văn bản pháp luật.", "LegalDocument", "All" }
                });

            // RolePermissions cho permission 89 đã tồn tại trong DB — chỉ insert Write(91).
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 91, 1 },
                    { 91, 2 },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 89, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 91, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 89, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 91, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 89, 3 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 89, 4 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 91);
        }
    }
}
