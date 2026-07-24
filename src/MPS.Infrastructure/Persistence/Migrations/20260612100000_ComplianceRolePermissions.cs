using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lỗi #31 (E2E 2026-07-21): mọi <c>GET /api/compliance/*</c> trả 403 với MỌI role, kể cả
    /// SystemAdmin — 20 quyền tuân thủ (Id 65–88) không được gán cho role nào.
    ///
    /// <para><b>Gốc rễ thật.</b> File này đã tồn tại từ 2026-06-12 nhưng CHƯA BAO GIỜ chạy: nó
    /// thiếu cặp attribute <c>[DbContext]</c> + <c>[Migration]</c>. EF Core nhận diện một
    /// migration qua attribute đó — thường nằm ở file <c>.Designer.cs</c> do scaffolder sinh,
    /// hoặc khai trực tiếp trên class. File này không có Designer VÀ không có attribute ⇒ EF
    /// không thấy nó ⇒ không có dòng nào trong <c>__EFMigrationsHistory</c>.</para>
    ///
    /// <para><b>Đính chính chẩn đoán cũ:</b> báo cáo E2E từng ghi nguyên nhân là "thiếu file
    /// .Designer.cs". Không đúng — 27 migration khác trong thư mục này cũng không có Designer mà
    /// vẫn chạy bình thường vì chúng khai <c>[Migration]</c> ngay trên class (ví dụ
    /// <c>20260721090000_AddLegalDocumentReadPermission</c>). File này là file DUY NHẤT thiếu CẢ
    /// HAI. Thêm Designer sẽ không giải quyết được gì.</para>
    ///
    /// <para><b>Idempotent.</b> Bản cũ dùng <c>InsertData</c> — sẽ vỡ khoá chính trên môi trường
    /// đã được vá tay ở mức dữ liệu trong lúc kiểm thử. Nay dùng SQL <c>NOT EXISTS</c> và join
    /// theo <c>Permissions.Resource</c>/<c>Action</c> thay vì hardcode Id, để không phụ thuộc
    /// việc đánh số Id giữ nguyên qua các đợt tái cấu trúc catalog quyền (bản cũ còn cấp cả
    /// <c>OtRate</c> 73–76 — resource đã bị gỡ ở migration
    /// <c>20260616082959_Compliance_RemoveOtRate_AddLegalDocument</c>).</para>
    ///
    /// <para>Ma trận (khớp <c>RolePermissionEntityConfiguration.AllPermissionIds</c>):
    /// SystemAdmin(1) + HR(2) full CRUD; Kế toán(3) + Giám đốc(4) read-only.</para>
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260612100000_ComplianceRolePermissions")]
    public partial class ComplianceRolePermissions : Migration
    {
        /// <summary>5 nhóm dữ liệu tuân thủ được cấp quyền theo ma trận trên.</summary>
        private static readonly string[] ComplianceResources =
        {
            "SocialInsuranceRate",
            "PitRegulation",
            "MinimumWage",
            "AllowanceExemptThreshold",
            "LaborLawConstants",
        };

        private const string FullCrudRoleIds = "1,2";   // SystemAdmin, HR
        private const string ReadOnlyRoleIds = "3,4";   // Kế toán, Giám đốc

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var resource in ComplianceResources)
            {
                migrationBuilder.Sql(GrantSql(resource, FullCrudRoleIds, readOnly: false));
                migrationBuilder.Sql(GrantSql(resource, ReadOnlyRoleIds, readOnly: true));
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var resource in ComplianceResources)
            {
                migrationBuilder.Sql($@"
DELETE rp
FROM RolePermissions rp
INNER JOIN Permissions p ON p.Id = rp.PermissionId
WHERE p.Resource = '{resource}'
  AND rp.RoleId IN ({FullCrudRoleIds},{ReadOnlyRoleIds});");
            }
        }

        /// <summary>
        /// Cấp quyền của <paramref name="resource"/> cho danh sách role, bỏ qua cặp đã tồn tại.
        /// <paramref name="readOnly"/> = true thì chỉ cấp action <c>Read</c>.
        /// </summary>
        private static string GrantSql(string resource, string roleIds, bool readOnly) => $@"
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.Id
FROM Permissions p
CROSS JOIN (SELECT CAST(value AS INT) AS RoleId FROM STRING_SPLIT('{roleIds}', ',')) r
WHERE p.Resource = '{resource}'
  {(readOnly ? "AND p.Action = 'Read'" : string.Empty)}
  AND NOT EXISTS (
        SELECT 1 FROM RolePermissions rp
        WHERE rp.PermissionId = p.Id AND rp.RoleId = r.RoleId);";
    }
}
