using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260726090000_PayrollReadSelfForHumanRoles")]
    public partial class PayrollReadSelfForHumanRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Payroll.Read.Self (253) trước đây chỉ seed cho SystemAdmin (1) và Accountant (3)
            // (migration 20260715091022_PayrollRolePermissions) — chưa có endpoint nào dùng nên
            // không ai nhận ra. Giờ GET /api/payroll/me/payslips (dashboard NV xem phiếu lương
            // của chính mình) cần quyền này cho MỌI người dùng là con người: ai cũng là nhân viên
            // và được xem lương của chính mình.
            //
            // Bỏ AiService (10) — service account, không gắn nhân viên. Dùng NOT EXISTS để chạy
            // lại nhiều lần vẫn an toàn (pattern 20260721150000_SelfAttendancePermissionsForAllRoles).
            migrationBuilder.Sql(@"
INSERT INTO [RolePermissions] ([PermissionId], [RoleId])
SELECT 253, r.[Id]
FROM [Roles] r
WHERE r.[Id] <> 10
  AND NOT EXISTS (
      SELECT 1 FROM [RolePermissions] rp
      WHERE rp.[PermissionId] = 253 AND rp.[RoleId] = r.[Id]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Trả về trạng thái cũ: chỉ SystemAdmin (1) và Accountant (3) giữ Payroll.Read.Self.
            migrationBuilder.Sql(@"
DELETE FROM [RolePermissions]
WHERE [PermissionId] = 253 AND [RoleId] NOT IN (1, 3);");
        }
    }
}
