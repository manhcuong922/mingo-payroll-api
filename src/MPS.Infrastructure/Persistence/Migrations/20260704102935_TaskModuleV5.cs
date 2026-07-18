using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskModuleV5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTasks_Users_AssignedToUserId",
                table: "ProjectTasks");

            migrationBuilder.DropTable(
                name: "TaskDailyLogs");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 158, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 158, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 158, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 161, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 170, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 159, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 162, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 171, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 160, 7 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 158, 9 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 161, 9 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 170, 9 });

            migrationBuilder.RenameColumn(
                name: "AssignedToUserId",
                table: "ProjectTasks",
                newName: "CompletedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectTasks_AssignedToUserId",
                table: "ProjectTasks",
                newName: "IX_ProjectTasks_CompletedByUserId");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "ProjectTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedHours",
                table: "ProjectTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProgressType",
                table: "ProjectTasks",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProgressUpdatedAt",
                table: "ProjectTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresHandover",
                table: "ProjectTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WasLate",
                table: "ProjectTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompletedByUserId",
                table: "Projects",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionNote",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultProgressType",
                table: "ProcessSteps",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ProductionAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    ObjectKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Bucket = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hex = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UploadedByUserId = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskAssignees",
                columns: table => new
                {
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAssignees", x => new { x.TaskId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TaskAssignees_ProjectTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "ProjectTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskAssignees_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 158,
                columns: new[] { "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[] { "Complete", "Project.Complete.Department", "Đóng đơn của xưởng mình.", "Project", "Department" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 159,
                columns: new[] { "Code", "Description", "Scope" },
                values: new object[] { "Task.Read.All", "Xem mọi task.", "All" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 160,
                columns: new[] { "Code", "Description", "Scope" },
                values: new object[] { "Task.Read.Department", "Xem task của xưởng mình.", "Department" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 161,
                columns: new[] { "Code", "Description", "Scope" },
                values: new object[] { "Task.Read.Assigned", "Xem task được giao cho mình.", "Assigned" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 162,
                columns: new[] { "Action", "Code", "Description", "Scope" },
                values: new object[] { "Read", "Task.Read.Confidential", "Xem task nhạy cảm.", "Confidential" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 163,
                columns: new[] { "Action", "Code", "Description" },
                values: new object[] { "Create", "Task.Create.Department", "Tạo task (1 người / nhóm) trong xưởng mình." });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 166,
                columns: new[] { "Action", "Code", "Description" },
                values: new object[] { "UpdateProgress", "Task.UpdateProgress.Department", "Cập nhật sản lượng task xưởng mình." });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 167,
                columns: new[] { "Action", "Code", "Description" },
                values: new object[] { "Complete", "Task.Complete.Department", "Hoàn thành task xưởng mình." });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 169,
                columns: new[] { "Action", "Code", "Description", "Resource" },
                values: new object[] { "Read", "ProductionAttachment.Read.Department", "Xem tài liệu/ảnh của xưởng mình.", "ProductionAttachment" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 170,
                columns: new[] { "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[] { "Write", "ProductionAttachment.Write.Department", "Tải lên/xóa tài liệu của xưởng mình.", "ProductionAttachment", "Department" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 171,
                columns: new[] { "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[] { "Read", "ProductionAttachment.Read.All", "Xem tài liệu/ảnh mọi xưởng.", "ProductionAttachment", "All" });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[,]
                {
                    { 168, "Delete", "Task.Delete.Department", "Xóa task lá của xưởng mình.", "Task", "Department" },
                    { 172, "Audit.Read", "Task.Audit.Read.All", "Xem lịch sử chỉnh sửa task/đơn mọi xưởng.", "Task", "All" },
                    { 173, "Audit.Read", "Task.Audit.Read.Department", "Xem lịch sử chỉnh sửa task/đơn xưởng mình.", "Task", "Department" }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 159, 2 },
                    { 159, 3 },
                    { 159, 4 },
                    { 162, 4 },
                    { 148, 6 },
                    { 154, 6 },
                    { 155, 6 },
                    { 157, 6 },
                    { 158, 6 },
                    { 160, 6 },
                    { 170, 6 },
                    { 161, 7 },
                    { 169, 7 },
                    { 159, 9 },
                    { 162, 9 },
                    { 168, 1 },
                    { 172, 1 },
                    { 173, 1 },
                    { 172, 4 },
                    { 168, 6 },
                    { 173, 6 },
                    { 172, 9 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_Status",
                table: "ProjectTasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CompletedByUserId",
                table: "Projects",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionAttachments_OwnerType_OwnerId",
                table: "ProductionAttachments",
                columns: new[] { "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionAttachments_UploadedByUserId",
                table: "ProductionAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignees_UserId",
                table: "TaskAssignees",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Users_CompletedByUserId",
                table: "Projects",
                column: "CompletedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTasks_Users_CompletedByUserId",
                table: "ProjectTasks",
                column: "CompletedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Users_CompletedByUserId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTasks_Users_CompletedByUserId",
                table: "ProjectTasks");

            migrationBuilder.DropTable(
                name: "ProductionAttachments");

            migrationBuilder.DropTable(
                name: "TaskAssignees");

            migrationBuilder.DropIndex(
                name: "IX_ProjectTasks_Status",
                table: "ProjectTasks");

            migrationBuilder.DropIndex(
                name: "IX_Projects_CompletedByUserId",
                table: "Projects");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 168, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 172, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 173, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 159, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 159, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 159, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 162, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 172, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 148, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 154, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 155, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 157, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 158, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 160, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 168, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 170, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 173, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 161, 7 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 169, 7 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 159, 9 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 162, 9 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 172, 9 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 168);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 172);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 173);

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "EstimatedHours",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "ProgressType",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "ProgressUpdatedAt",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "RequiresHandover",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "WasLate",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CompletedByUserId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CompletionNote",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DefaultProgressType",
                table: "ProcessSteps");

            migrationBuilder.RenameColumn(
                name: "CompletedByUserId",
                table: "ProjectTasks",
                newName: "AssignedToUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectTasks_CompletedByUserId",
                table: "ProjectTasks",
                newName: "IX_ProjectTasks_AssignedToUserId");

            migrationBuilder.CreateTable(
                name: "TaskDailyLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoggedByUserId = table.Column<int>(type: "int", nullable: false),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    WorkerUserId = table.Column<int>(type: "int", nullable: false),
                    LoggedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuantityDone = table.Column<int>(type: "int", nullable: false),
                    TargetQuantity = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskDailyLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskDailyLogs_ProjectTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "ProjectTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskDailyLogs_Users_LoggedByUserId",
                        column: x => x.LoggedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskDailyLogs_Users_WorkerUserId",
                        column: x => x.WorkerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 158,
                columns: new[] { "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[] { "Read", "Task.Read.All", "Xem mọi task.", "Task", "All" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 159,
                columns: new[] { "Code", "Description", "Scope" },
                values: new object[] { "Task.Read.Department", "Xem task của xưởng mình.", "Department" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 160,
                columns: new[] { "Code", "Description", "Scope" },
                values: new object[] { "Task.Read.Assigned", "Xem task được giao cho mình.", "Assigned" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 161,
                columns: new[] { "Code", "Description", "Scope" },
                values: new object[] { "Task.Read.Confidential", "Xem task nhạy cảm.", "Confidential" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 162,
                columns: new[] { "Action", "Code", "Description", "Scope" },
                values: new object[] { "Create", "Task.Create.AdHoc", "Tạo task thủ công.", "AdHoc" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 163,
                columns: new[] { "Action", "Code", "Description" },
                values: new object[] { "BulkCreate", "Task.BulkCreate.Department", "Tạo hàng loạt task, chia SL cho nhiều người." });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 166,
                columns: new[] { "Action", "Code", "Description" },
                values: new object[] { "Reassign", "Task.Reassign.Department", "Chuyển assign task trong xưởng mình." });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 167,
                columns: new[] { "Action", "Code", "Description" },
                values: new object[] { "LogDaily", "Task.LogDaily.Department", "Chốt sản lượng cuối ngày." });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 169,
                columns: new[] { "Action", "Code", "Description", "Resource" },
                values: new object[] { "Delete", "Task.Delete.Department", "Xóa task lá của xưởng mình.", "Task" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 170,
                columns: new[] { "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[] { "Audit.Read", "Task.Audit.Read.All", "Xem lịch sử chỉnh sửa task/đơn mọi xưởng.", "Task", "All" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 171,
                columns: new[] { "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[] { "Audit.Read", "Task.Audit.Read.Department", "Xem lịch sử chỉnh sửa task/đơn xưởng mình.", "Task", "Department" });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 158, 2 },
                    { 158, 3 },
                    { 158, 4 },
                    { 161, 4 },
                    { 170, 4 },
                    { 159, 6 },
                    { 162, 6 },
                    { 171, 6 },
                    { 160, 7 },
                    { 158, 9 },
                    { 161, 9 },
                    { 170, 9 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskDailyLogs_LoggedByUserId",
                table: "TaskDailyLogs",
                column: "LoggedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDailyLogs_TaskId_WorkDate",
                table: "TaskDailyLogs",
                columns: new[] { "TaskId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskDailyLogs_WorkerUserId_WorkDate",
                table: "TaskDailyLogs",
                columns: new[] { "WorkerUserId", "WorkDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTasks_Users_AssignedToUserId",
                table: "ProjectTasks",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
