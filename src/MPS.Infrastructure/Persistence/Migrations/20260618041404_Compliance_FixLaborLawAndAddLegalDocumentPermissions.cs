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
            // LegalDocument.Read.All / LegalDocument.Write.All were never seeded — the
            // LegalDocumentController endpoints have been unreachable by any role.
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[,]
                {
                    { 89, "Read",  "LegalDocument.Read.All",  "Xem văn bản pháp luật.", "LegalDocument", "All" },
                    { 90, "Write", "LegalDocument.Write.All", "Tạo/cập nhật/liên kết văn bản pháp luật.", "LegalDocument", "All" }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    // SystemAdmin (1) + HR (2) — full Read/Write
                    { 89, 1 }, { 90, 1 },
                    { 89, 2 }, { 90, 2 },

                    // Accountant (3) + Director (4) — Read-only
                    { 89, 3 },
                    { 89, 4 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 89, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 90, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 89, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 90, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 89, 3 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 89, 4 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValues: new object[] { 89, 90 });
        }
    }
}
