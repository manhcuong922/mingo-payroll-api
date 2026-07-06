using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260704183000_AttendanceEditLockSettings")]
    public partial class AttendanceEditLockSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsEditLockEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DailyEditCutoffTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    LockAfterDays = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AllowAdminOverride = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RequireOverrideReason = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceSettings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceSettings_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSettings_CreatedByUserId",
                table: "AttendanceSettings",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSettings_UpdatedByUserId",
                table: "AttendanceSettings",
                column: "UpdatedByUserId");

            migrationBuilder.Sql("""
                INSERT INTO [AttendanceSettings]
                    ([IsEditLockEnabled], [DailyEditCutoffTime], [LockAfterDays],
                     [AllowAdminOverride], [RequireOverrideReason], [CreatedAt])
                SELECT
                    CAST(0 AS bit), CAST('18:00:00' AS time), 0,
                    CAST(1 AS bit), CAST(1 AS bit), SYSUTCDATETIME()
                WHERE NOT EXISTS (SELECT 1 FROM [AttendanceSettings]);
                """);

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                columnTypes: new[] { "int", "nvarchar(50)", "nvarchar(100)", "nvarchar(255)", "nvarchar(50)", "nvarchar(20)" },
                values: new object[,]
                {
                    { 141, "Read", "AttendanceSetting.Read.All", "Xem thiết lập khóa sửa dữ liệu chấm công.", "AttendanceSetting", "All" },
                    { 142, "Update", "AttendanceSetting.Update.All", "Cập nhật thiết lập khóa sửa dữ liệu chấm công.", "AttendanceSetting", "All" },
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                columnTypes: new[] { "int", "int" },
                values: new object[,]
                {
                    { 1, 141 }, { 1, 142 },
                    { 2, 141 }, { 2, 142 },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [RolePermissions] WHERE [PermissionId] IN (141, 142);");
            migrationBuilder.Sql("DELETE FROM [Permissions] WHERE [Id] IN (141, 142);");

            migrationBuilder.DropTable(
                name: "AttendanceSettings");
        }
    }
}
