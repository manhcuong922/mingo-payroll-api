using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WfhRequestModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WfhRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeInformationId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalDays = table.Column<decimal>(type: "decimal(4,1)", nullable: false),
                    WorkPlan = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_WfhRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WfhRequests_EmployeeInformations_EmployeeInformationId",
                        column: x => x.EmployeeInformationId,
                        principalTable: "EmployeeInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WfhRequests_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WfhRequests_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WfhRequests_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WfhRequests_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WfhRequests_CancelledByUserId",
                table: "WfhRequests",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WfhRequests_CreatedByUserId",
                table: "WfhRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WfhRequests_EmployeeInformationId_StartDate_EndDate",
                table: "WfhRequests",
                columns: new[] { "EmployeeInformationId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WfhRequests_ReviewedByUserId",
                table: "WfhRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WfhRequests_Status_ApproverRoute",
                table: "WfhRequests",
                columns: new[] { "Status", "ApproverRoute" });

            migrationBuilder.CreateIndex(
                name: "IX_WfhRequests_UpdatedByUserId",
                table: "WfhRequests",
                column: "UpdatedByUserId");

            // Permission seed 240–244
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                columnTypes: new[] { "int", "nvarchar(50)", "nvarchar(100)", "nvarchar(255)", "nvarchar(50)", "nvarchar(20)" },
                values: new object[,]
                {
                    { 240, "Read",    "WfhRequest.Read.All",     "Xem tất cả đơn WFH.",       "WfhRequest", "All"  },
                    { 241, "Read",    "WfhRequest.Read.Self",    "Xem đơn WFH của mình.",     "WfhRequest", "Self" },
                    { 242, "Create",  "WfhRequest.Create.Self",  "Tạo đơn WFH cho mình.",     "WfhRequest", "Self" },
                    { 243, "Approve", "WfhRequest.Approve.All",  "Duyệt/từ chối đơn WFH.",    "WfhRequest", "All"  },
                    { 244, "Cancel",  "WfhRequest.Cancel.Self",  "Tự hủy đơn WFH.",           "WfhRequest", "Self" },
                });

            migrationBuilder.Sql("""
                INSERT INTO [RolePermissions] ([RoleId], [PermissionId]) VALUES
                    (1, 240), (1, 241), (1, 242), (1, 243), (1, 244),
                    (2, 240), (2, 241), (2, 242), (2, 243), (2, 244),
                    (5, 240), (5, 241), (5, 242), (5, 243), (5, 244);

                INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
                SELECT DISTINCT rp.[RoleId], perm.[PermissionId]
                FROM [RolePermissions] rp
                CROSS JOIN (VALUES (241), (242), (244)) AS perm([PermissionId])
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
            migrationBuilder.Sql("DELETE FROM [RolePermissions] WHERE [PermissionId] BETWEEN 240 AND 244;");
            migrationBuilder.Sql("DELETE FROM [Permissions] WHERE [Id] BETWEEN 240 AND 244;");

            migrationBuilder.DropTable(
                name: "WfhRequests");
        }
    }
}
