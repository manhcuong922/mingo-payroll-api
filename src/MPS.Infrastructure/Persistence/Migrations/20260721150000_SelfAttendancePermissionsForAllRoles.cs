using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260721150000_SelfAttendancePermissionsForAllRoles")]
    public partial class SelfAttendancePermissionsForAllRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Attendance.Read.Self (107) và EmployeeShift.Read.Self (179) trước đây chỉ cấp cho
            // role Employee (7). Nhưng MỌI người dùng là con người đều đồng thời là nhân viên và
            // phải xem được ca làm + tình trạng chấm công CỦA CHÍNH MÌNH (dashboard kiosk gọi
            // GET /employee-shifts/me và GET /attendance/me). HR/Director/... đang bị 403.
            //
            // Chỉ cấp cho role người thật — bỏ AiService (10) vì là service account, không gắn
            // với nhân viên nào. Dùng NOT EXISTS để chạy lại nhiều lần vẫn an toàn.
            migrationBuilder.Sql(@"
INSERT INTO [RolePermissions] ([PermissionId], [RoleId])
SELECT p.[PermissionId], r.[Id]
FROM (VALUES (107), (179)) AS p([PermissionId])
CROSS JOIN [Roles] r
WHERE r.[Id] <> 10
  AND NOT EXISTS (
      SELECT 1 FROM [RolePermissions] rp
      WHERE rp.[PermissionId] = p.[PermissionId] AND rp.[RoleId] = r.[Id]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Trả về trạng thái cũ: chỉ Employee (7) giữ 2 quyền self.
            migrationBuilder.Sql(@"
DELETE FROM [RolePermissions]
WHERE [PermissionId] IN (107, 179) AND [RoleId] <> 7;");
        }
    }
}
