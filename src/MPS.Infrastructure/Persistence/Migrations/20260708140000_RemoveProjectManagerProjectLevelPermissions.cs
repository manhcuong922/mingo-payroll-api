using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260708140000_RemoveProjectManagerProjectLevelPermissions")]
    public partial class RemoveProjectManagerProjectLevelPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- PM (RoleId=6) không còn chạm vào cấp Đơn hàng ở bất kỳ thao tác nào: không tự tạo đơn, không áp quy
-- trình sinh task (Project.GenerateTasks), không sửa (Project.Update.All), không hoàn thành/đóng đơn
-- (Project.Complete.Department). Chỉ còn Project.Read.Department (đã bị FilterByAssignedPm giới hạn
-- còn công đoạn của mình) và mọi thao tác ở cấp Công đoạn/Task (Task.*.Department).
-- ProcessTemplate.ReadForApply.All (213) chỉ dùng để hỗ trợ Project.GenerateTasks nên bỏ luôn.
DELETE FROM [RolePermissions] WHERE [RoleId] = 6 AND [PermissionId] IN (
    (SELECT [Id] FROM [Permissions] WHERE [Code] = N'Project.Update.All'),
    (SELECT [Id] FROM [Permissions] WHERE [Code] = N'Project.GenerateTasks'),
    (SELECT [Id] FROM [Permissions] WHERE [Code] = N'Project.Complete.Department'),
    (SELECT [Id] FROM [Permissions] WHERE [Code] = N'ProcessTemplate.ReadForApply.All')
);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO [RolePermissions] ([PermissionId], [RoleId])
SELECT [Id], 6 FROM [Permissions] WHERE [Code] IN (
    N'Project.Update.All', N'Project.GenerateTasks', N'Project.Complete.Department',
    N'ProcessTemplate.ReadForApply.All'
);
");
        }
    }
}
