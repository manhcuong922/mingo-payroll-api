using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ComplianceAutoUpdate_CrawlTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComplianceCrawledDocumentLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DocumentUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    UrlHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DocumentNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExtractStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceCrawledDocumentLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceSourceSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceSourceSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceUpdateProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DocumentUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DocumentNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TargetEntityType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProposedValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ExtractedJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiffJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValidationReport = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceUpdateProposals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCrawledDocumentLogs_SourceKey",
                table: "ComplianceCrawledDocumentLogs",
                column: "SourceKey");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCrawledDocumentLogs_UrlHash",
                table: "ComplianceCrawledDocumentLogs",
                column: "UrlHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceSourceSnapshots_SourceKey",
                table: "ComplianceSourceSnapshots",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceUpdateProposals_Status_CreatedAt",
                table: "ComplianceUpdateProposals",
                columns: new[] { "Status", "CreatedAt" });

            // ===== Permission master cho module Auto-Update (ID 89–90; 87/88 đã dùng cho LaborLawConstants seed master) =====
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[,]
                {
                    { 89, "Run",  "ComplianceAutoUpdate.Run.All",  "Kích hoạt quét & đề xuất cập nhật pháp luật tự động.", "ComplianceAutoUpdate", "All" },
                    { 90, "Read", "ComplianceAutoUpdate.Read.All", "Xem các đề xuất cập nhật pháp luật tự động.", "ComplianceAutoUpdate", "All" }
                });

            // Gán quyền: Run cho SystemAdmin(1) + HR(2); Read cho cả 4 role chuẩn (Accountant=3, Director=4 đọc).
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 89, 1 }, { 89, 2 },
                    { 90, 1 }, { 90, 2 }, { 90, 3 }, { 90, 4 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 89, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 89, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 90, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 90, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 90, 3 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 90, 4 });
            migrationBuilder.DeleteData(table: "Permissions", keyColumn: "Id", keyValue: 89);
            migrationBuilder.DeleteData(table: "Permissions", keyColumn: "Id", keyValue: 90);

            migrationBuilder.DropTable(
                name: "ComplianceCrawledDocumentLogs");

            migrationBuilder.DropTable(
                name: "ComplianceSourceSnapshots");

            migrationBuilder.DropTable(
                name: "ComplianceUpdateProposals");
        }
    }
}
