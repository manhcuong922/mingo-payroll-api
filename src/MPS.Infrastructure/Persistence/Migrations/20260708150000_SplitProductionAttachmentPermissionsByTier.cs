using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260708150000_SplitProductionAttachmentPermissionsByTier")]
    public partial class SplitProductionAttachmentPermissionsByTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Tách ProductionAttachment.Read/Write.Department (dùng chung mọi tầng) thành 2 cặp riêng theo tầng:
--   .Project (tầng 1, Đơn hàng) — chỉ Director/DeputyDirector/DepartmentManager.
--   .Stage   (tầng 2+3, Công đoạn/Task lá — quyền giống nhau) — thêm ProjectManager (ghi được),
--            Employee chỉ đọc (không ghi).
SET IDENTITY_INSERT [Permissions] ON;
INSERT INTO [Permissions] ([Id], [Action], [Code], [Description], [Resource], [Scope])
VALUES
    (220, N'Read',  N'ProductionAttachment.Read.Project',  N'Xem tài liệu/ảnh của đơn hàng (tầng 1).', N'ProductionAttachment', N'Project'),
    (221, N'Write', N'ProductionAttachment.Write.Project', N'Đăng tải/xóa tài liệu của đơn hàng (tầng 1).', N'ProductionAttachment', N'Project'),
    (222, N'Read',  N'ProductionAttachment.Read.Stage',    N'Xem tài liệu/ảnh của Công đoạn/Task (tầng 2+3).', N'ProductionAttachment', N'Stage'),
    (223, N'Write', N'ProductionAttachment.Write.Stage',   N'Đăng tải/xóa tài liệu của Công đoạn/Task (tầng 2+3).', N'ProductionAttachment', N'Stage');
SET IDENTITY_INSERT [Permissions] OFF;

-- Director (4) + DeputyDirector (9): xem/đăng cả 2 tầng.
INSERT INTO [RolePermissions] ([PermissionId], [RoleId])
VALUES
    (220, 4), (221, 4), (222, 4), (223, 4),
    (220, 9), (221, 9), (222, 9), (223, 9),
    -- DepartmentManager (5): xem/đăng cả 2 tầng của xưởng mình quản lý.
    (220, 5), (221, 5), (222, 5), (223, 5),
    -- ProjectManager (6): CHỈ tầng 2+3 — không xem/đăng tài liệu cấp Đơn hàng.
    (222, 6), (223, 6),
    -- Employee (7): chỉ xem tầng 2+3, không đăng tải.
    (222, 7);

-- Gỡ permission cũ (dùng chung mọi tầng) khỏi mọi role — thay thế hoàn toàn bởi permission theo tầng ở trên.
DELETE FROM [RolePermissions] WHERE [PermissionId] IN (
    (SELECT [Id] FROM [Permissions] WHERE [Code] = N'ProductionAttachment.Read.Department'),
    (SELECT [Id] FROM [Permissions] WHERE [Code] = N'ProductionAttachment.Write.Department')
);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Khôi phục đúng seed gốc: Director(4)/DeputyDirector(9) chỉ có Write.Department (đọc dùng ReadAll
-- bypass); ProjectManager(6) có cả Read+Write.Department; Employee(7) chỉ Read.Department;
-- DepartmentManager(5) không có permission attachment nào (trạng thái trước migration này).
INSERT INTO [RolePermissions] ([PermissionId], [RoleId])
SELECT [Id], 4 FROM [Permissions] WHERE [Code] = N'ProductionAttachment.Write.Department'
UNION ALL SELECT [Id], 9 FROM [Permissions] WHERE [Code] = N'ProductionAttachment.Write.Department'
UNION ALL SELECT [Id], 6 FROM [Permissions] WHERE [Code] IN (N'ProductionAttachment.Read.Department', N'ProductionAttachment.Write.Department')
UNION ALL SELECT [Id], 7 FROM [Permissions] WHERE [Code] = N'ProductionAttachment.Read.Department';

DELETE FROM [RolePermissions] WHERE [PermissionId] IN (220, 221, 222, 223);
DELETE FROM [Permissions] WHERE [Id] IN (220, 221, 222, 223);
");
        }
    }
}
