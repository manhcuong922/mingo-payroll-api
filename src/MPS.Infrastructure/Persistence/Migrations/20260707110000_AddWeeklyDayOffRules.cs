using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260707110000_AddWeeklyDayOffRules")]
    public partial class AddWeeklyDayOffRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeeklyDayOffRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DaysOfWeekMask = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ApplyScope = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "AllEmployees"),
                    TargetDepartmentIds = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TargetEmployeeIds = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyDayOffRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyDayOffRules_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeeklyDayOffRules_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyDayOffRules_CreatedByUserId",
                table: "WeeklyDayOffRules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyDayOffRules_IsActive_EffectiveFrom_EffectiveTo",
                table: "WeeklyDayOffRules",
                columns: new[] { "IsActive", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyDayOffRules_UpdatedByUserId",
                table: "WeeklyDayOffRules",
                column: "UpdatedByUserId");

            // ===== Seed permissions (IDs 174–177) — WeeklyDayOff module =====
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [Permissions] ON;
INSERT INTO [Permissions] ([Id], [Action], [Code], [Description], [Resource], [Scope]) VALUES
    (174, N'Read',   N'WeeklyDayOff.Read.All',   N'Xem danh sách rule ngày nghỉ tuần.',    N'WeeklyDayOff', N'All'),
    (175, N'Create', N'WeeklyDayOff.Create.All', N'Tạo rule ngày nghỉ tuần.',              N'WeeklyDayOff', N'All'),
    (176, N'Update', N'WeeklyDayOff.Update.All', N'Cập nhật rule ngày nghỉ tuần.',         N'WeeklyDayOff', N'All'),
    (177, N'Delete', N'WeeklyDayOff.Delete.All', N'Xóa/deactivate rule ngày nghỉ tuần.',   N'WeeklyDayOff', N'All');
SET IDENTITY_INSERT [Permissions] OFF;");

            // ===== Role permissions =====
            // SystemAdmin (1) + HR (2): full CRUD
            // Director (4) + DepartmentManager (5): read-only (phase 1)
            migrationBuilder.Sql(@"
INSERT INTO [RolePermissions] ([PermissionId], [RoleId]) VALUES
    (174, 1), (175, 1), (176, 1), (177, 1),
    (174, 2), (175, 2), (176, 2), (177, 2),
    (174, 4),
    (174, 5);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM RolePermissions WHERE PermissionId BETWEEN 174 AND 177;");
            migrationBuilder.Sql("DELETE FROM Permissions WHERE Id BETWEEN 174 AND 177;");

            migrationBuilder.DropTable(
                name: "WeeklyDayOffRules");
        }
    }
}
