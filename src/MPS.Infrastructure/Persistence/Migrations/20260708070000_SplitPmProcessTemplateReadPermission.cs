using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitPmProcessTemplateReadPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [Permissions] ON;
INSERT INTO [Permissions] ([Id], [Action], [Code], [Description], [Resource], [Scope])
VALUES
    (213, N'ReadForApply', N'ProcessTemplate.ReadForApply.All', N'Xem quy trình mẫu để áp dụng khi sinh task cho đơn hàng (không vào được màn quản lý quy trình mẫu).', N'ProcessTemplate', N'All');
SET IDENTITY_INSERT [Permissions] OFF;

-- PM (RoleId=6) không còn tự tạo đơn hay quản lý quy trình mẫu — chỉ giữ quyền đọc quy trình để áp
-- dụng (Project.GenerateTasks vẫn còn, không đổi). Xóa 2 quyền cũ, thêm quyền đọc thay thế.
DELETE FROM [RolePermissions] WHERE [RoleId] = 6 AND [PermissionId] IN (
    (SELECT [Id] FROM [Permissions] WHERE [Code] = N'ProcessTemplate.Read.All'),
    (SELECT [Id] FROM [Permissions] WHERE [Code] = N'Project.Create.All')
);

INSERT INTO [RolePermissions] ([PermissionId], [RoleId])
VALUES
    (213, 6);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [RolePermissions] WHERE [PermissionId] = 213;
DELETE FROM [Permissions] WHERE [Id] = 213;

INSERT INTO [RolePermissions] ([PermissionId], [RoleId])
VALUES
    ((SELECT [Id] FROM [Permissions] WHERE [Code] = N'ProcessTemplate.Read.All'), 6),
    ((SELECT [Id] FROM [Permissions] WHERE [Code] = N'Project.Create.All'), 6);
");
        }
    }
}
