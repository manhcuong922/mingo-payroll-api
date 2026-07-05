using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskModuleSimplify : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTasks_Users_ApprovedByUserId",
                table: "ProjectTasks");

            migrationBuilder.DropTable(
                name: "ProcessStepDependencies");

            migrationBuilder.DropTable(
                name: "TaskDependencies");

            migrationBuilder.DropIndex(
                name: "IX_ProjectTasks_ApprovedByUserId",
                table: "ProjectTasks");

            migrationBuilder.DropIndex(
                name: "IX_ProjectTasks_AssignedToUserId_Status",
                table: "ProjectTasks");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 120, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 120, 6 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 120);

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "ProjectTasks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "ProjectTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByUserId",
                table: "ProjectTasks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProcessStepDependencies",
                columns: table => new
                {
                    StepId = table.Column<int>(type: "int", nullable: false),
                    PredecessorStepId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessStepDependencies", x => new { x.StepId, x.PredecessorStepId });
                    table.ForeignKey(
                        name: "FK_ProcessStepDependencies_ProcessSteps_PredecessorStepId",
                        column: x => x.PredecessorStepId,
                        principalTable: "ProcessSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessStepDependencies_ProcessSteps_StepId",
                        column: x => x.StepId,
                        principalTable: "ProcessSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskDependencies",
                columns: table => new
                {
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    PredecessorTaskId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskDependencies", x => new { x.TaskId, x.PredecessorTaskId });
                    table.ForeignKey(
                        name: "FK_TaskDependencies_ProjectTasks_PredecessorTaskId",
                        column: x => x.PredecessorTaskId,
                        principalTable: "ProjectTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskDependencies_ProjectTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "ProjectTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[] { 120, "Approve", "Task.Approve.Department", "Duyệt chuyển Done trong xưởng mình.", "Task", "Department" });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 120, 1 },
                    { 120, 6 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_ApprovedByUserId",
                table: "ProjectTasks",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_AssignedToUserId_Status",
                table: "ProjectTasks",
                columns: new[] { "AssignedToUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepDependencies_PredecessorStepId",
                table: "ProcessStepDependencies",
                column: "PredecessorStepId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependencies_PredecessorTaskId",
                table: "TaskDependencies",
                column: "PredecessorTaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTasks_Users_ApprovedByUserId",
                table: "ProjectTasks",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
