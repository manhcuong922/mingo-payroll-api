using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeDependentModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeDependents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeInformationId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Relationship = table.Column<byte>(type: "tinyint", nullable: false),
                    TaxIdentificationNumber = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    CitizenId = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    RegisteredFromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDependents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDependents_EmployeeInformations_EmployeeInformationId",
                        column: x => x.EmployeeInformationId,
                        principalTable: "EmployeeInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDependents_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDependents_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DependentRegistrationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeInformationId = table.Column<int>(type: "int", nullable: false),
                    RequestType = table.Column<byte>(type: "tinyint", nullable: false),
                    TargetDependentId = table.Column<int>(type: "int", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Relationship = table.Column<byte>(type: "tinyint", nullable: true),
                    TaxIdentificationNumber = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    CitizenId = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    RegisteredFromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RegisteredToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SupportingDocumentUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_DependentRegistrationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DependentRegistrationRequests_EmployeeDependents_TargetDependentId",
                        column: x => x.TargetDependentId,
                        principalTable: "EmployeeDependents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DependentRegistrationRequests_EmployeeInformations_EmployeeInformationId",
                        column: x => x.EmployeeInformationId,
                        principalTable: "EmployeeInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DependentRegistrationRequests_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DependentRegistrationRequests_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DependentRegistrationRequests_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DependentRegistrationRequests_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DependentRegistrationRequests_CancelledByUserId",
                table: "DependentRegistrationRequests",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DependentRegistrationRequests_CreatedByUserId",
                table: "DependentRegistrationRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DependentRegistrationRequests_EmployeeInformationId_Status",
                table: "DependentRegistrationRequests",
                columns: new[] { "EmployeeInformationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DependentRegistrationRequests_ReviewedByUserId",
                table: "DependentRegistrationRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DependentRegistrationRequests_Status",
                table: "DependentRegistrationRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DependentRegistrationRequests_TargetDependentId",
                table: "DependentRegistrationRequests",
                column: "TargetDependentId");

            migrationBuilder.CreateIndex(
                name: "IX_DependentRegistrationRequests_UpdatedByUserId",
                table: "DependentRegistrationRequests",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDependents_CitizenId",
                table: "EmployeeDependents",
                column: "CitizenId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDependents_CreatedByUserId",
                table: "EmployeeDependents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDependents_EmployeeInformationId_IsActive",
                table: "EmployeeDependents",
                columns: new[] { "EmployeeInformationId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDependents_TaxIdentificationNumber",
                table: "EmployeeDependents",
                column: "TaxIdentificationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDependents_UpdatedByUserId",
                table: "EmployeeDependents",
                column: "UpdatedByUserId");

            // Permission seed 245–251
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                columnTypes: new[] { "int", "nvarchar(50)", "nvarchar(100)", "nvarchar(255)", "nvarchar(50)", "nvarchar(20)" },
                values: new object[,]
                {
                    { 245, "Read",    "DependentRequest.Read.All",    "Xem tất cả đơn đăng ký người phụ thuộc.", "DependentRequest", "All"  },
                    { 246, "Read",    "DependentRequest.Read.Self",   "Xem đơn của mình.",                       "DependentRequest", "Self" },
                    { 247, "Create",  "DependentRequest.Create.Self", "Tạo đơn đăng ký/cập nhật.",               "DependentRequest", "Self" },
                    { 248, "Approve", "DependentRequest.Approve.All", "HR duyệt/từ chối đơn.",                   "DependentRequest", "All"  },
                    { 249, "Cancel",  "DependentRequest.Cancel.Self", "Tự hủy đơn Pending.",                     "DependentRequest", "Self" },
                    { 250, "Read",    "EmployeeDependent.Read.All",   "HR xem tất cả người phụ thuộc.",          "EmployeeDependent", "All"  },
                    { 251, "Read",    "EmployeeDependent.Read.Self",  "Employee xem người phụ thuộc của mình.",  "EmployeeDependent", "Self" },
                });

            // Admin (1) + HR (2): full quyền. Employee (role có Create.Self của Leave = 116): Self perms.
            migrationBuilder.Sql("""
                INSERT INTO [RolePermissions] ([RoleId], [PermissionId]) VALUES
                    (1, 245), (1, 246), (1, 247), (1, 248), (1, 249), (1, 250), (1, 251),
                    (2, 245), (2, 246), (2, 247), (2, 248), (2, 249), (2, 250), (2, 251);

                INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
                SELECT DISTINCT rp.[RoleId], perm.[PermissionId]
                FROM [RolePermissions] rp
                CROSS JOIN (VALUES (246), (247), (249), (251)) AS perm([PermissionId])
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
            migrationBuilder.Sql("DELETE FROM [RolePermissions] WHERE [PermissionId] BETWEEN 245 AND 251;");
            migrationBuilder.Sql("DELETE FROM [Permissions] WHERE [Id] BETWEEN 245 AND 251;");

            migrationBuilder.DropTable(
                name: "DependentRegistrationRequests");

            migrationBuilder.DropTable(
                name: "EmployeeDependents");
        }
    }
}
