using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LateEarlyRequestModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LateEarlyRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeInformationId = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    Minutes = table.Column<short>(type: "smallint", nullable: false),
                    IsUnplanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeductFromLeaveBalance = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ApproverRoute = table.Column<byte>(type: "tinyint", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByUserId = table.Column<int>(type: "int", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LateEarlyRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LateEarlyRequests_EmployeeInformations_EmployeeInformationId",
                        column: x => x.EmployeeInformationId,
                        principalTable: "EmployeeInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LateEarlyRequests_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LateEarlyRequests_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LateEarlyRequests_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LateEarlyRequests_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LateEarlyRequests_CancelledByUserId",
                table: "LateEarlyRequests",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LateEarlyRequests_CreatedByUserId",
                table: "LateEarlyRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LateEarlyRequests_EmployeeInformationId_WorkDate_Status",
                table: "LateEarlyRequests",
                columns: new[] { "EmployeeInformationId", "WorkDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LateEarlyRequests_ReviewedByUserId",
                table: "LateEarlyRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LateEarlyRequests_Status_ApproverRoute",
                table: "LateEarlyRequests",
                columns: new[] { "Status", "ApproverRoute" });

            migrationBuilder.CreateIndex(
                name: "IX_LateEarlyRequests_UpdatedByUserId",
                table: "LateEarlyRequests",
                column: "UpdatedByUserId");

            // Seed permission (IDs 235–239) + gán role
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                columnTypes: new[] { "int", "nvarchar(50)", "nvarchar(100)", "nvarchar(255)", "nvarchar(50)", "nvarchar(20)" },
                values: new object[,]
                {
                    { 235, "Read",    "LateEarlyRequest.Read.All",     "Xem tất cả đơn đi trễ/về sớm.",        "LateEarlyRequest", "All"  },
                    { 236, "Read",    "LateEarlyRequest.Read.Self",    "Xem đơn đi trễ/về sớm của mình.",       "LateEarlyRequest", "Self" },
                    { 237, "Create",  "LateEarlyRequest.Create.Self",  "Tạo đơn đi trễ/về sớm cho mình.",       "LateEarlyRequest", "Self" },
                    { 238, "Approve", "LateEarlyRequest.Approve.All",  "Duyệt/từ chối đơn đi trễ/về sớm.",      "LateEarlyRequest", "All"  },
                    { 239, "Cancel",  "LateEarlyRequest.Cancel.Self",  "Tự hủy đơn đi trễ/về sớm.",             "LateEarlyRequest", "Self" },
                });

            // Read.All + Approve.All: Admin (1), HR (2), DepartmentManager (5).
            // Read.Self + Create.Self + Cancel.Self: gán cho mọi role đã có LeaveRequest.Create.Self (116).
            migrationBuilder.Sql("""
                INSERT INTO [RolePermissions] ([RoleId], [PermissionId]) VALUES
                    (1, 235), (1, 236), (1, 237), (1, 238), (1, 239),
                    (2, 235), (2, 236), (2, 237), (2, 238), (2, 239),
                    (5, 235), (5, 236), (5, 237), (5, 238), (5, 239);

                INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
                SELECT DISTINCT rp.[RoleId], perm.[PermissionId]
                FROM [RolePermissions] rp
                CROSS JOIN (VALUES (236), (237), (239)) AS perm([PermissionId])
                WHERE rp.[PermissionId] = 116
                  AND NOT EXISTS (
                      SELECT 1 FROM [RolePermissions] existing
                      WHERE existing.[RoleId] = rp.[RoleId]
                        AND existing.[PermissionId] = perm.[PermissionId]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [RolePermissions] WHERE [PermissionId] BETWEEN 235 AND 239;");
            migrationBuilder.Sql("DELETE FROM [Permissions] WHERE [Id] BETWEEN 235 AND 239;");

            migrationBuilder.DropTable(
                name: "LateEarlyRequests");
        }
    }
}
