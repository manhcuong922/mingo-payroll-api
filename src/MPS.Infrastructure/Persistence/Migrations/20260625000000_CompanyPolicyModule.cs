// Hand-authored migration cho module nội quy công ty.
// Snapshot existing project có pre-existing issue (AuditLog.AppliedAttendanceRuleId
// chưa khớp), nên không thể `dotnet ef migrations add` để regen tự động — file
// này được viết tay và áp DB bằng `dotnet ef database update`.
//
// Sau khi snapshot issue được fix, có thể chạy `dotnet ef migrations add
// CompanyPolicyModule_Snapshot` để đồng bộ snapshot mà không sinh schema mới.

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompanyPolicyModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Tables ────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "CompanyPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyPolicies_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyPolicies_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyPolicyFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyPolicyId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MinioBucket = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MinioObjectKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "int", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyPolicyFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyPolicyFiles_CompanyPolicies_CompanyPolicyId",
                        column: x => x.CompanyPolicyId,
                        principalTable: "CompanyPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyPolicyFiles_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ── 2. Indexes ───────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_CompanyPolicies_IsActive",
                table: "CompanyPolicies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPolicies_CreatedByUserId",
                table: "CompanyPolicies",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPolicies_UpdatedByUserId",
                table: "CompanyPolicies",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPolicyFiles_CompanyPolicyId_Version",
                table: "CompanyPolicyFiles",
                columns: new[] { "CompanyPolicyId", "Version" },
                unique: true);

            // Filtered unique: only 1 file IsCurrent per policy.
            migrationBuilder.CreateIndex(
                name: "IX_CompanyPolicyFiles_CompanyPolicyId_IsCurrent",
                table: "CompanyPolicyFiles",
                column: "CompanyPolicyId",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPolicyFiles_UploadedByUserId",
                table: "CompanyPolicyFiles",
                column: "UploadedByUserId");

            // ── 3. Permissions (IDs 130–133) ─────────────────────────────────
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[,]
                {
                    { 130, "Read",   "CompanyPolicy.Read.All",   "Xem nội quy công ty.",                          "CompanyPolicy", "All" },
                    { 131, "Create", "CompanyPolicy.Create.All", "Tạo nội quy mới + upload version đầu tiên.",    "CompanyPolicy", "All" },
                    { 132, "Update", "CompanyPolicy.Update.All", "Đổi tên/mô tả + upload version mới.",          "CompanyPolicy", "All" },
                    { 133, "Delete", "CompanyPolicy.Delete.All", "Xoá hoặc deactivate nội quy.",                  "CompanyPolicy", "All" },
                });

            // ── 4. Role mappings ─────────────────────────────────────────────
            // Read.All (130): mọi role 1–7 (NV cũng xem được).
            // Create/Update/Delete: chỉ SystemAdmin (1) + HR (2).
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 130, 1 }, { 130, 2 }, { 130, 3 }, { 130, 4 }, { 130, 5 }, { 130, 6 }, { 130, 7 },
                    { 131, 1 }, { 131, 2 },
                    { 132, 1 }, { 132, 2 },
                    { 133, 1 }, { 133, 2 },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (permissionId, roleId) in new[]
            {
                (130, 1), (130, 2), (130, 3), (130, 4), (130, 5), (130, 6), (130, 7),
                (131, 1), (131, 2),
                (132, 1), (132, 2),
                (133, 1), (133, 2),
            })
            {
                migrationBuilder.DeleteData(
                    table: "RolePermissions",
                    keyColumns: new[] { "PermissionId", "RoleId" },
                    keyValues: new object[] { permissionId, roleId });
            }

            migrationBuilder.DeleteData(table: "Permissions", keyColumn: "Id", keyValue: 130);
            migrationBuilder.DeleteData(table: "Permissions", keyColumn: "Id", keyValue: 131);
            migrationBuilder.DeleteData(table: "Permissions", keyColumn: "Id", keyValue: 132);
            migrationBuilder.DeleteData(table: "Permissions", keyColumn: "Id", keyValue: 133);

            migrationBuilder.DropTable(name: "CompanyPolicyFiles");
            migrationBuilder.DropTable(name: "CompanyPolicies");
        }
    }
}
