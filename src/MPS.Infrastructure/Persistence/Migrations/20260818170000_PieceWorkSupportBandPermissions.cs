using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Phase 2 — quyền xem/sửa bảng bậc hỗ trợ theo tiến độ của HĐ khoán việc.
    /// Cùng khuôn với <c>BhxhTaxPaymentRolePermissions</c>: idempotent bằng NOT EXISTS trên
    /// <c>Code</c>, không đụng schema.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260818170000_PieceWorkSupportBandPermissions")]
    public partial class PieceWorkSupportBandPermissions : Migration
    {
        private static readonly (int Id, string Code, string Resource, string Action, string Scope, string Description)[] NewPermissions =
        {
            (270, "PieceWorkSupportBand.Read.All",   "PieceWorkSupportBand", "Read",   "All", "Xem bảng bậc hỗ trợ theo tiến độ của hợp đồng khoán việc."),
            (271, "PieceWorkSupportBand.Update.All", "PieceWorkSupportBand", "Update", "All", "Sửa bảng bậc hỗ trợ theo tiến độ của hợp đồng khoán việc."),
        };

        // SystemAdmin(1) + HR(2) + Accountant(3) + Director(4) — giống bảng hệ số năng suất.
        private static readonly int[] TargetRoleIds = { 1, 2, 3, 4 };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (id, code, resource, action, scope, description) in NewPermissions)
            {
                var descriptionEscaped = description.Replace("'", "''");
                migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Code = '{code}')
BEGIN
    SET IDENTITY_INSERT Permissions ON;
    INSERT INTO Permissions (Id, Code, Resource, Action, Scope, Description)
    VALUES ({id}, '{code}', '{resource}', '{action}', '{scope}', N'{descriptionEscaped}');
    SET IDENTITY_INSERT Permissions OFF;
END;");

                var roleValues = string.Join(", ", TargetRoleIds.Select(r => $"({r})"));
                migrationBuilder.Sql($@"
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.Id
FROM (VALUES {roleValues}) AS r(RoleId)
CROSS JOIN (SELECT Id FROM Permissions WHERE Code = '{code}') AS p
WHERE NOT EXISTS (
    SELECT 1 FROM RolePermissions rp
    WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.Id);");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (_, code, _, _, _, _) in NewPermissions)
            {
                migrationBuilder.Sql($@"
DELETE rp FROM RolePermissions rp
INNER JOIN Permissions p ON p.Id = rp.PermissionId
WHERE p.Code = '{code}';
DELETE FROM Permissions WHERE Code = '{code}';");
            }
        }
    }
}
