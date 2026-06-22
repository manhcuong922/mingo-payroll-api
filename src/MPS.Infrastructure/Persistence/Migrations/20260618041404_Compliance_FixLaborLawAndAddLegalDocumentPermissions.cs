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
            // NOTE: Id 89 đã bị AuditLog.Read.All chiếm (migration 20260616100727).
            // LegalDocument dùng 90/91 — khớp PermissionIds (AuditLog=89, LegalDocument=90/91).
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[,]
                {
                    { 90, "Read",  "LegalDocument.Read.All",  "Xem văn bản pháp luật.", "LegalDocument", "All" },
                    { 91, "Write", "LegalDocument.Write.All", "Tạo/cập nhật/liên kết văn bản pháp luật.", "LegalDocument", "All" }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    // SystemAdmin (1) + HR (2) — full Read/Write
                    { 90, 1 }, { 91, 1 },
                    { 90, 2 }, { 91, 2 },

                    // Accountant (3) + Director (4) — Read-only
                    { 90, 3 },
                    { 90, 4 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 90, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 91, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 90, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 91, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 90, 3 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 90, 4 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValues: new object[] { 90, 91 });
        }
    }
}
