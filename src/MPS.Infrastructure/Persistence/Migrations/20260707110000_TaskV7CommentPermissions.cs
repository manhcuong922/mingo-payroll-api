using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskV7CommentPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [Permissions] ON;
INSERT INTO [Permissions] ([Id], [Action], [Code], [Description], [Resource], [Scope])
VALUES
    (209, N'CreateStage', N'Task.CreateStage.Department', N'Tạo Công đoạn (task cha) trong xưởng mình.', N'Task', N'Department'),
    (210, N'Read', N'Comment.Read.All', N'Xem bình luận mọi tầng, mọi xưởng.', N'Comment', N'All'),
    (211, N'Create', N'Comment.Create.Own', N'Viết bình luận trên task của chính mình.', N'Comment', N'Own'),
    (212, N'Create', N'Comment.Create.Department', N'Viết bình luận theo phạm vi xưởng mình.', N'Comment', N'Department');
SET IDENTITY_INSERT [Permissions] OFF;

INSERT INTO [RolePermissions] ([PermissionId], [RoleId])
VALUES
    (209, 4),
    (209, 5),
    (209, 9),
    (210, 4),
    (210, 9),
    (211, 7),
    (212, 5),
    (212, 6);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [RolePermissions] WHERE [PermissionId] IN (209, 210, 211, 212);
DELETE FROM [Permissions] WHERE [Id] IN (209, 210, 211, 212);
");
        }
    }
}
