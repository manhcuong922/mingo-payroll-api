using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260708110000_OvertimeRequestBatchWorkflow")]
    public partial class OvertimeRequestBatchWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Batch workflow columns — nullable để legacy records không vỡ
            migrationBuilder.AddColumn<int>(
                name: "ProjectId",
                table: "OvertimeRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "ApproverRoute",
                table: "OvertimeRequests",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "OvertimeRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_ProjectId",
                table: "OvertimeRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_BatchId",
                table: "OvertimeRequests",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_Status_ApproverRoute",
                table: "OvertimeRequests",
                columns: new[] { "Status", "ApproverRoute" });

            migrationBuilder.AddForeignKey(
                name: "FK_OvertimeRequests_Projects_ProjectId",
                table: "OvertimeRequests",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ===== Seed permission 178 =====
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [Permissions] ON;
INSERT INTO [Permissions] ([Id], [Action], [Code], [Description], [Resource], [Scope]) VALUES
    (178, N'CreateBatch', N'OvertimeRequest.CreateBatch.Department',
     N'PM tạo batch OT request cho NV cùng phòng ban (scope theo Project).',
     N'OvertimeRequest', N'Department');
SET IDENTITY_INSERT [Permissions] OFF;");

            // Cấp cho ProjectManager (role 6)
            migrationBuilder.Sql(@"
INSERT INTO [RolePermissions] ([PermissionId], [RoleId]) VALUES (178, 6);");

            // ===== Xóa quyền tự xin OT của Employee (permission 112) =====
            // Constant giữ ở Permissions.cs để migration cũ không vỡ, nhưng
            // KHÔNG role nào còn được cấp — luồng self-request bị đóng hoàn toàn.
            migrationBuilder.Sql(@"
DELETE FROM [RolePermissions] WHERE [PermissionId] = 112;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback: khôi phục quyền tự xin OT cho Employee (role 7) — best-effort
            // vì không biết chắc trước đó có phải chỉ Employee có, nhưng đúng cho state
            // sau migration đầu tiên land permission 112.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [PermissionId] = 112 AND [RoleId] = 7)
    INSERT INTO [RolePermissions] ([PermissionId], [RoleId]) VALUES (112, 7);");

            migrationBuilder.Sql(@"
DELETE FROM [RolePermissions] WHERE [PermissionId] = 178;
DELETE FROM [Permissions] WHERE [Id] = 178;");

            migrationBuilder.DropForeignKey(
                name: "FK_OvertimeRequests_Projects_ProjectId",
                table: "OvertimeRequests");

            migrationBuilder.DropIndex(
                name: "IX_OvertimeRequests_Status_ApproverRoute",
                table: "OvertimeRequests");

            migrationBuilder.DropIndex(
                name: "IX_OvertimeRequests_BatchId",
                table: "OvertimeRequests");

            migrationBuilder.DropIndex(
                name: "IX_OvertimeRequests_ProjectId",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "ApproverRoute",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "OvertimeRequests");
        }
    }
}
