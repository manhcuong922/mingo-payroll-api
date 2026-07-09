using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Refactor task-full-generation: cây quy trình N tầng (ProcessStep self-ref + headcount/position/ratio/scale),
    /// đơn có ResponsibleUserId + BaseQuantity template, task có PlannedHeadcount/RequiredPositionId/DeadlineOffsetRatio,
    /// bỏ per-stage PM (AssignedPmUserId). Đổi tên role 5→Planning Manager, 6→Production Team Leader + tái phân bổ quyền.
    /// </summary>
    public partial class TaskNTierAndRoleRealign : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===== ProcessTemplate =====
            migrationBuilder.AddColumn<int>(
                name: "BaseQuantity", table: "ProcessTemplates", type: "int", nullable: false, defaultValue: 1);

            // ===== ProcessStep — cây N tầng + authoring vị trí lá =====
            migrationBuilder.AddColumn<int>(
                name: "ParentProcessStepId", table: "ProcessSteps", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "PlannedHeadcount", table: "ProcessSteps", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(
                name: "RequiredPositionId", table: "ProcessSteps", type: "int", nullable: true);
            migrationBuilder.AddColumn<bool>(
                name: "ScalesWithQuantity", table: "ProcessSteps", type: "bit", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<double>(
                name: "DefaultDeadlineOffsetRatio", table: "ProcessSteps", type: "float", nullable: true);

            // ===== Project — tổ trưởng sản xuất chịu trách nhiệm =====
            migrationBuilder.AddColumn<int>(
                name: "ResponsibleUserId", table: "Projects", type: "int", nullable: true);

            // ===== ProjectTask — vị trí lá + cascade thời gian; bỏ per-stage PM =====
            migrationBuilder.AddColumn<int>(
                name: "PlannedHeadcount", table: "ProjectTasks", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(
                name: "RequiredPositionId", table: "ProjectTasks", type: "int", nullable: true);
            migrationBuilder.AddColumn<double>(
                name: "DeadlineOffsetRatio", table: "ProjectTasks", type: "float", nullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTasks_Users_AssignedPmUserId", table: "ProjectTasks");
            migrationBuilder.DropIndex(
                name: "IX_ProjectTasks_AssignedPmUserId", table: "ProjectTasks");
            migrationBuilder.DropColumn(
                name: "AssignedPmUserId", table: "ProjectTasks");

            // ===== Indexes + FKs mới =====
            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_ParentProcessStepId_StepOrder", table: "ProcessSteps",
                columns: new[] { "ParentProcessStepId", "StepOrder" });
            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_RequiredPositionId", table: "ProcessSteps", column: "RequiredPositionId");
            migrationBuilder.CreateIndex(
                name: "IX_Projects_ResponsibleUserId", table: "Projects", column: "ResponsibleUserId");
            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_RequiredPositionId", table: "ProjectTasks", column: "RequiredPositionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessSteps_ProcessSteps_ParentProcessStepId", table: "ProcessSteps",
                column: "ParentProcessStepId", principalTable: "ProcessSteps", principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_ProcessSteps_Positions_RequiredPositionId", table: "ProcessSteps",
                column: "RequiredPositionId", principalTable: "Positions", principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Users_ResponsibleUserId", table: "Projects",
                column: "ResponsibleUserId", principalTable: "Users", principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTasks_Positions_RequiredPositionId", table: "ProjectTasks",
                column: "RequiredPositionId", principalTable: "Positions", principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ===== Đổi tên role 5/6 (auth vẫn permission-based) =====
            migrationBuilder.UpdateData(
                table: "Roles", keyColumn: "Id", keyValue: 5,
                columns: new[] { "Name", "Description" },
                values: new object[]
                {
                    "Planning Manager",
                    "Planning Manager — lập kế hoạch, tạo đơn, gán người, đặt tổ trưởng, sửa timeline. Scope xưởng.",
                });
            migrationBuilder.UpdateData(
                table: "Roles", keyColumn: "Id", keyValue: 6,
                columns: new[] { "Name", "Description" },
                values: new object[]
                {
                    "Production Team Leader",
                    "Tổ trưởng sản xuất — chịu trách nhiệm đơn (ResponsibleUserId), cập nhật tiến độ + hoàn thành task/đơn.",
                });

            // ===== Tái phân bổ RolePermission (raw SQL, idempotent) =====
            // Role 5 (Planning Manager): cấp toàn bộ quyền lập kế hoạch + sản xuất (override tiến độ).
            migrationBuilder.Sql(@"
INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
SELECT 5, p.[Id] FROM [Permissions] p
WHERE p.[Code] IN (
    N'Project.Read.Department', N'Project.Create.All', N'Project.Update.All', N'Project.Delete.All',
    N'Project.GenerateTasks', N'Project.Complete.Department', N'ProcessTemplate.ReadForApply.All',
    N'Task.Create.Department', N'Task.CreateStage.Department', N'Task.Update.Department',
    N'Task.UpdateProgress.Department', N'Task.Complete.Department', N'Task.Delete.Department',
    N'Task.Audit.Read.Department'
) AND NOT EXISTS (
    SELECT 1 FROM [RolePermissions] rp WHERE rp.[RoleId] = 5 AND rp.[PermissionId] = p.[Id]);
");

            // Role 6 (Production Team Leader): chỉ tiến độ + hoàn thành + đọc. Cấp thêm progress/complete + đóng đơn.
            migrationBuilder.Sql(@"
INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
SELECT 6, p.[Id] FROM [Permissions] p
WHERE p.[Code] IN (
    N'Project.Read.Department', N'Task.Read.Department', N'Task.UpdateProgress.Department',
    N'Task.Complete.Department', N'Project.Complete.Department', N'Task.Audit.Read.Department',
    N'ProductionAttachment.Read.Stage', N'ProductionAttachment.Write.Stage'
) AND NOT EXISTS (
    SELECT 1 FROM [RolePermissions] rp WHERE rp.[RoleId] = 6 AND rp.[PermissionId] = p.[Id]);
");

            // Role 6: thu hồi quyền lập kế hoạch (tạo/sửa/xoá cấu trúc task) — chỉ Planning Manager mới có.
            migrationBuilder.Sql(@"
DELETE FROM [RolePermissions]
WHERE [RoleId] = 6 AND [PermissionId] IN (
    SELECT [Id] FROM [Permissions] WHERE [Code] IN (
        N'Task.Create.Department', N'Task.CreateStage.Department',
        N'Task.Update.Department', N'Task.Delete.Department'));
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hoàn quyền role 6 (lập kế hoạch task) + thu quyền role 5 mở rộng.
            migrationBuilder.Sql(@"
INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
SELECT 6, p.[Id] FROM [Permissions] p
WHERE p.[Code] IN (
    N'Task.Create.Department', N'Task.CreateStage.Department', N'Task.Update.Department', N'Task.Delete.Department'
) AND NOT EXISTS (
    SELECT 1 FROM [RolePermissions] rp WHERE rp.[RoleId] = 6 AND rp.[PermissionId] = p.[Id]);
");
            migrationBuilder.Sql(@"
DELETE FROM [RolePermissions]
WHERE [RoleId] = 5 AND [PermissionId] IN (
    SELECT [Id] FROM [Permissions] WHERE [Code] IN (
        N'Project.Read.Department', N'Project.Create.All', N'Project.Update.All', N'Project.Delete.All',
        N'Project.GenerateTasks', N'Project.Complete.Department', N'ProcessTemplate.ReadForApply.All',
        N'Task.Create.Department', N'Task.CreateStage.Department', N'Task.Update.Department',
        N'Task.UpdateProgress.Department', N'Task.Complete.Department', N'Task.Delete.Department',
        N'Task.Audit.Read.Department'));
");
            migrationBuilder.Sql(@"
DELETE FROM [RolePermissions]
WHERE [RoleId] = 6 AND [PermissionId] IN (
    SELECT [Id] FROM [Permissions] WHERE [Code] IN (
        N'Task.UpdateProgress.Department', N'Task.Complete.Department', N'Project.Complete.Department'));
");

            migrationBuilder.UpdateData(
                table: "Roles", keyColumn: "Id", keyValue: 5,
                columns: new[] { "Name", "Description" },
                values: new object[] { "DepartmentManager", "Trưởng phòng — quản lý KPI nhân viên trong phòng." });
            migrationBuilder.UpdateData(
                table: "Roles", keyColumn: "Id", keyValue: 6,
                columns: new[] { "Name", "Description" },
                values: new object[] { "ProjectManager", "Quản lý dự án — theo dõi KPI dự án." });

            migrationBuilder.DropForeignKey(name: "FK_ProcessSteps_ProcessSteps_ParentProcessStepId", table: "ProcessSteps");
            migrationBuilder.DropForeignKey(name: "FK_ProcessSteps_Positions_RequiredPositionId", table: "ProcessSteps");
            migrationBuilder.DropForeignKey(name: "FK_Projects_Users_ResponsibleUserId", table: "Projects");
            migrationBuilder.DropForeignKey(name: "FK_ProjectTasks_Positions_RequiredPositionId", table: "ProjectTasks");

            migrationBuilder.DropIndex(name: "IX_ProcessSteps_ParentProcessStepId_StepOrder", table: "ProcessSteps");
            migrationBuilder.DropIndex(name: "IX_ProcessSteps_RequiredPositionId", table: "ProcessSteps");
            migrationBuilder.DropIndex(name: "IX_Projects_ResponsibleUserId", table: "Projects");
            migrationBuilder.DropIndex(name: "IX_ProjectTasks_RequiredPositionId", table: "ProjectTasks");

            migrationBuilder.AddColumn<int>(
                name: "AssignedPmUserId", table: "ProjectTasks", type: "int", nullable: true);
            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_AssignedPmUserId", table: "ProjectTasks", column: "AssignedPmUserId");
            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTasks_Users_AssignedPmUserId", table: "ProjectTasks",
                column: "AssignedPmUserId", principalTable: "Users", principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(name: "PlannedHeadcount", table: "ProjectTasks");
            migrationBuilder.DropColumn(name: "RequiredPositionId", table: "ProjectTasks");
            migrationBuilder.DropColumn(name: "DeadlineOffsetRatio", table: "ProjectTasks");
            migrationBuilder.DropColumn(name: "ResponsibleUserId", table: "Projects");
            migrationBuilder.DropColumn(name: "ParentProcessStepId", table: "ProcessSteps");
            migrationBuilder.DropColumn(name: "PlannedHeadcount", table: "ProcessSteps");
            migrationBuilder.DropColumn(name: "RequiredPositionId", table: "ProcessSteps");
            migrationBuilder.DropColumn(name: "ScalesWithQuantity", table: "ProcessSteps");
            migrationBuilder.DropColumn(name: "DefaultDeadlineOffsetRatio", table: "ProcessSteps");
            migrationBuilder.DropColumn(name: "BaseQuantity", table: "ProcessTemplates");
        }
    }
}
