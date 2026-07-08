using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260708130000_AddFullDepartmentReadPermissions")]
    public partial class AddFullDepartmentReadPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [Permissions] ON;
INSERT INTO [Permissions] ([Id], [Action], [Code], [Description], [Resource], [Scope])
VALUES
    (218, N'Read', N'Project.Read.FullDepartment', N'Xem toàn bộ đơn/công đoạn/task của 1 xưởng (3 tầng), không giới hạn theo AssignedPmUserId.', N'Project', N'FullDepartment'),
    (219, N'Read', N'Task.Read.FullDepartment', N'Xem toàn bộ Công đoạn + task con trong 1 xưởng, không bị ẩn theo AssignedPmUserId.', N'Task', N'FullDepartment');
SET IDENTITY_INSERT [Permissions] OFF;

-- DepartmentManager (RoleId=5): trước đây bị gỡ hết quyền Project/Task (DepartmentManagerDropProductionPerms).
-- Giờ gán lại quyền quan sát ĐẦY ĐỦ 3 tầng của xưởng mình quản lý (không phải toàn công ty như GĐ,
-- không phải chỉ Công đoạn được gán như ProjectManager) — cần đi kèm gán EmployeeInformation.DepartmentId
-- cho user role này để IsManagerOfAsync xác định đúng xưởng.
INSERT INTO [RolePermissions] ([PermissionId], [RoleId])
VALUES
    (218, 5),
    (219, 5);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [RolePermissions] WHERE [PermissionId] IN (218, 219);
DELETE FROM [Permissions] WHERE [Id] IN (218, 219);
");
        }
    }
}
