using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lỗi #14 (E2E 2026-07-21): mọi endpoint <c>GET /api/compliance/legal-documents</c> trả 403 với
    /// MỌI role, kể cả SystemAdmin. Nguyên nhân: catalog quyền chỉ có <c>LegalDocument.Write.All</c>
    /// (Id 91); <c>LegalDocument.Read.All</c> — quyền mà LegalDocumentController yêu cầu — CHƯA BAO GIỜ
    /// được insert. Migration 20260618041404 giả định nó "đã có sẵn Id=89 từ seed trước" nhưng seed đó
    /// không tồn tại trong repo ⇒ guard đọc không bao giờ thoả.
    ///
    /// Sửa: insert quyền vào Id 89 (đang trống trong DB — AuditLog.Read.All nằm ở 90) và gán cho
    /// SystemAdmin(1) + HR(2) + Kế toán(3) + Giám đốc(4), đồng bộ với ma trận read-only của các bảng
    /// tuân thủ khác (xem 20260612100000_ComplianceRolePermissions).
    ///
    /// Idempotent: dùng SQL có NOT EXISTS thay vì InsertData, vì một số môi trường đã được vá tay.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260721090000_AddLegalDocumentReadPermission")]
    public partial class AddLegalDocumentReadPermission : Migration
    {
        private const string PermissionCode = "LegalDocument.Read.All";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Code = '{PermissionCode}')
BEGIN
    SET IDENTITY_INSERT Permissions ON;
    INSERT INTO Permissions (Id, Code, Resource, Action, Scope, Description)
    VALUES (89, '{PermissionCode}', 'LegalDocument', 'Read', 'All',
            N'Xem danh mục văn bản pháp luật và liên kết với phiên bản tuân thủ.');
    SET IDENTITY_INSERT Permissions OFF;
END;");

            // Gán cho SystemAdmin/HR/Kế toán/Giám đốc — bám theo Code để không phụ thuộc Id.
            migrationBuilder.Sql($@"
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.Id
FROM (VALUES (1), (2), (3), (4)) AS r(RoleId)
CROSS JOIN (SELECT Id FROM Permissions WHERE Code = '{PermissionCode}') AS p
WHERE NOT EXISTS (
    SELECT 1 FROM RolePermissions rp
    WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.Id);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
DELETE rp FROM RolePermissions rp
JOIN Permissions p ON p.Id = rp.PermissionId
WHERE p.Code = '{PermissionCode}';

DELETE FROM Permissions WHERE Code = '{PermissionCode}';");
        }
    }
}
