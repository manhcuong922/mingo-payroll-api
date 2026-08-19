using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Quyền cho nhóm chứng từ chi trả: xuất lệnh chi lương / ủy nhiệm chi nộp BHXH /
    /// tờ khai QTT-TNCN, và xem-sửa cấu hình chi trả cấp công ty.
    ///
    /// <para>Cùng khuôn với <c>PieceWorkSupportBandPermissions</c>: idempotent bằng NOT EXISTS
    /// trên <c>Code</c>, không đụng schema.</para>
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260819140000_PayrollPaymentExportPermissions")]
    public partial class PayrollPaymentExportPermissions : Migration
    {
        private static readonly (int Id, string Code, string Resource, string Action, string Scope, string Description, int[] RoleIds)[] NewPermissions =
        {
            // Xuất chứng từ = cầm số tiền và số tài khoản của toàn bộ nhân sự ⇒ chỉ kế toán
            // và quản trị, KHÔNG mở cho HR như các quyền nhân sự thông thường.
            (272, "PayrollDocument.Export.All", "PayrollDocument", "Export", "All",
                "Xuất chứng từ chi trả từ kỳ lương: lệnh chi lương, ủy nhiệm chi nộp BHXH, tờ khai quyết toán thuế TNCN.",
                new[] { 1, 3, 4 }),

            (273, "CompanyPaymentSetting.Read.All", "CompanyPaymentSetting", "Read", "All",
                "Xem cấu hình chi trả của công ty (tài khoản ngân hàng, mã đơn vị BHXH).",
                new[] { 1, 3, 4 }),

            // Sửa cấu hình = đổi được tài khoản nhận tiền trên mọi chứng từ về sau ⇒ hẹp nhất.
            (274, "CompanyPaymentSetting.Update.All", "CompanyPaymentSetting", "Update", "All",
                "Sửa cấu hình chi trả của công ty (tài khoản ngân hàng, mã đơn vị BHXH).",
                new[] { 1, 3 }),
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (id, code, resource, action, scope, description, roleIds) in NewPermissions)
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

                var roleValues = string.Join(", ", roleIds.Select(r => $"({r})"));
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
            foreach (var (_, code, _, _, _, _, _) in NewPermissions)
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
