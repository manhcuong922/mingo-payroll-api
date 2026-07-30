using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Seed 4 permission mới của module BHXH & Tax Payment Tracking (IDs 266–269):
    /// <list type="bullet">
    ///   <item>266 — Payroll.Lock.All (chốt cuối cùng, khoá kỳ lương)</item>
    ///   <item>267 — BhxhTaxPayment.Read.All</item>
    ///   <item>268 — BhxhTaxPayment.Record.All (ghi nhận nộp + upload evidence)</item>
    ///   <item>269 — BhxhTaxPayment.Confirm.All (xác nhận chấp nhận chênh lệch)</item>
    /// </list>
    /// Seed cho SystemAdmin(1) + HR(2) + Kế toán(3) + Giám đốc(4) — role khác có thể cấu hình sau
    /// qua RBAC. Idempotent: dùng SQL NOT EXISTS + SET IDENTITY_INSERT (theo pattern
    /// 20260721090000_AddLegalDocumentReadPermission).
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260724125903_BhxhTaxPaymentRolePermissions")]
    public partial class BhxhTaxPaymentRolePermissions : Migration
    {
        private static readonly (int Id, string Code, string Resource, string Action, string Scope, string Description)[] NewPermissions =
        {
            (266, "Payroll.Lock.All",             "Payroll",         "Lock",    "All", "Khoá kỳ lương (Finalized → Locked), kích hoạt sinh nghĩa vụ nộp BHXH/TNCN."),
            (267, "BhxhTaxPayment.Read.All",      "BhxhTaxPayment",  "Read",    "All", "Xem danh sách và chi tiết nghĩa vụ nộp BHXH & TNCN của mọi kỳ."),
            (268, "BhxhTaxPayment.Record.All",    "BhxhTaxPayment",  "Record",  "All", "Ghi nhận đã nộp BHXH/TNCN và upload ảnh bằng chứng."),
            (269, "BhxhTaxPayment.Confirm.All",   "BhxhTaxPayment",  "Confirm", "All", "Xác nhận chấp nhận số tiền chênh lệch giữa thực nộp và AmountDue."),
        };

        // SystemAdmin(1) + HR(2) + Accountant(3) + Director(4).
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
JOIN Permissions p ON p.Id = rp.PermissionId
WHERE p.Code = '{code}';

DELETE FROM Permissions WHERE Code = '{code}';");
            }
        }
    }
}
