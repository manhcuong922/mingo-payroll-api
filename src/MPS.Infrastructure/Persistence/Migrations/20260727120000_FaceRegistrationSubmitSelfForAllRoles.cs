using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260727120000_FaceRegistrationSubmitSelfForAllRoles")]
    public partial class FaceRegistrationSubmitSelfForAllRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FaceRegistration.Submit.Self (134) trước đây chỉ cấp cho SystemAdmin (1), HR (2)
            // và Employee (7). Nhưng MỌI người dùng là con người đều đồng thời là nhân viên và
            // phải tự đăng ký được khuôn mặt của CHÍNH MÌNH — Tổ trưởng (6), Quản đốc (5),
            // Giám đốc (4), Kế toán (3), Phó giám đốc (9) đang bị 403 ở cả
            // POST /attendance/face-registrations lẫn GET /attendance/face-registrations/my
            // (hai endpoint này dùng chung quyền 134).
            //
            // Cùng lớp lỗi đã fix ở 20260721150000 (107, 179) và 20260726090000 (253) — lần đó
            // bỏ sót 134. Không đụng 135/136: xem toàn bộ và duyệt vẫn là việc của HR/Admin.
            //
            // Chỉ cấp cho role người thật — bỏ AiService (10) vì là service account, không gắn
            // với nhân viên nào. Dùng NOT EXISTS để chạy lại nhiều lần vẫn an toàn.
            //
            // Lưu ý vận hành: PermissionAuthorizationHandler chỉ đọc claim "perms" trong JWT và
            // PermissionResolver cache role→permissions 5 phút, nên người dùng phải ĐĂNG NHẬP LẠI
            // sau khi migration chạy thì token mới có 134.
            migrationBuilder.Sql(@"
INSERT INTO [RolePermissions] ([PermissionId], [RoleId])
SELECT p.[PermissionId], r.[Id]
FROM (VALUES (134)) AS p([PermissionId])
CROSS JOIN [Roles] r
WHERE r.[Id] <> 10
  AND NOT EXISTS (
      SELECT 1 FROM [RolePermissions] rp
      WHERE rp.[PermissionId] = p.[PermissionId] AND rp.[RoleId] = r.[Id]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Trả về trạng thái cũ: chỉ SystemAdmin (1), HR (2), Employee (7) giữ quyền 134.
            migrationBuilder.Sql(@"
DELETE FROM [RolePermissions]
WHERE [PermissionId] = 134 AND [RoleId] NOT IN (1, 2, 7);");
        }
    }
}
