using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskV7FixCreateStageRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TaskV7CommentPermissions (2026-07-07 11:00) gán nhầm Task.CreateStage.Department (209) và
            // Comment.Create.Department (212) cho DepartmentManager (role 5) — nhưng
            // DepartmentManagerDropProductionPerms (2026-07-04) đã rút MỌI quyền Project/Task khác của role 5
            // (role này "GỘP VAI", không tham gia luồng sản xuất — xem RolePermissionEntityConfiguration.cs).
            // Người thực sự đóng vai "quản lý xưởng" tạo Công đoạn/Comment trong module Task là ProjectManager
            // (role 6, tổ trưởng xưởng = Department.ManagerId) — chuyển 2 permission này về đúng role.
            migrationBuilder.Sql(@"
DELETE FROM [RolePermissions] WHERE [PermissionId] = 209 AND [RoleId] = 5;
DELETE FROM [RolePermissions] WHERE [PermissionId] = 212 AND [RoleId] = 5;

IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [PermissionId] = 209 AND [RoleId] = 6)
    INSERT INTO [RolePermissions] ([PermissionId], [RoleId]) VALUES (209, 6);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [RolePermissions] WHERE [PermissionId] = 209 AND [RoleId] = 6;

IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [PermissionId] = 209 AND [RoleId] = 5)
    INSERT INTO [RolePermissions] ([PermissionId], [RoleId]) VALUES (209, 5);
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [PermissionId] = 212 AND [RoleId] = 5)
    INSERT INTO [RolePermissions] ([PermissionId], [RoleId]) VALUES (212, 5);
");
        }
    }
}
