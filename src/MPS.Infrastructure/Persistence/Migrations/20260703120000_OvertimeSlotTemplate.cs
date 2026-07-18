using System;
using MPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260703120000_OvertimeSlotTemplate")]
    public partial class OvertimeSlotTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OvertimeSlotTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    IsNightSlot = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MultiplierNormalDay = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    MultiplierWeeklyRestDay = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    MultiplierHolidayDay = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    MultiplierNormalNightNoPrevOt = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    MultiplierNormalNightWithPrevOt = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    MultiplierWeeklyRestNight = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    MultiplierHolidayNight = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OvertimeSlotTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OvertimeSlotTemplates_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OvertimeSlotTemplates_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeSlotTemplates_Code",
                table: "OvertimeSlotTemplates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeSlotTemplates_IsActive",
                table: "OvertimeSlotTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeSlotTemplates_CreatedByUserId",
                table: "OvertimeSlotTemplates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeSlotTemplates_UpdatedByUserId",
                table: "OvertimeSlotTemplates",
                column: "UpdatedByUserId");

            migrationBuilder.AddColumn<int>(
                name: "OvertimeSlotTemplateId",
                table: "OvertimeRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OtDayHours",
                table: "OvertimeRequests",
                type: "decimal(4,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OtNightHours",
                table: "OvertimeRequests",
                type: "decimal(4,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_OvertimeSlotTemplateId",
                table: "OvertimeRequests",
                column: "OvertimeSlotTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_OvertimeRequests_OvertimeSlotTemplates_OvertimeSlotTemplateId",
                table: "OvertimeRequests",
                column: "OvertimeSlotTemplateId",
                principalTable: "OvertimeSlotTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                columnTypes: new[] { "int", "nvarchar(50)", "nvarchar(100)", "nvarchar(255)", "nvarchar(50)", "nvarchar(20)" },
                values: new object[,]
                {
                    { 137, "Read",   "OvertimeSlotTemplate.Read.All",   "Xem danh sách khung giờ làm thêm có sẵn.", "OvertimeSlotTemplate", "All" },
                    { 138, "Create", "OvertimeSlotTemplate.Create.All", "Tạo khung giờ làm thêm mới.",              "OvertimeSlotTemplate", "All" },
                    { 139, "Update", "OvertimeSlotTemplate.Update.All", "Sửa khung giờ làm thêm.",                  "OvertimeSlotTemplate", "All" },
                    { 140, "Delete", "OvertimeSlotTemplate.Delete.All", "Xoá hoặc deactivate khung giờ.",           "OvertimeSlotTemplate", "All" },
                });

            // Read.All (137): mọi role 1–7 (Employee cần đọc để chọn slot khi đăng ký OT).
            // Create/Update/Delete: SystemAdmin (1) + HR (2) + DepartmentManager (5).
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                columnTypes: new[] { "int", "int" },
                values: new object[,]
                {
                    { 137, 1 }, { 137, 2 }, { 137, 3 }, { 137, 4 }, { 137, 5 }, { 137, 6 }, { 137, 7 },
                    { 138, 1 }, { 138, 2 }, { 138, 5 },
                    { 139, 1 }, { 139, 2 }, { 139, 5 },
                    { 140, 1 }, { 140, 2 }, { 140, 5 },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (permissionId, roleId) in new[]
            {
                (137, 1), (137, 2), (137, 3), (137, 4), (137, 5), (137, 6), (137, 7),
                (138, 1), (138, 2), (138, 5),
                (139, 1), (139, 2), (139, 5),
                (140, 1), (140, 2), (140, 5),
            })
            {
                migrationBuilder.DeleteData(
                    table: "RolePermissions",
                    keyColumns: new[] { "PermissionId", "RoleId" },
                    keyValues: new object[] { permissionId, roleId });
            }

            migrationBuilder.DeleteData(table: "Permissions", keyColumn: "Id", keyValue: 137);
            migrationBuilder.DeleteData(table: "Permissions", keyColumn: "Id", keyValue: 138);
            migrationBuilder.DeleteData(table: "Permissions", keyColumn: "Id", keyValue: 139);
            migrationBuilder.DeleteData(table: "Permissions", keyColumn: "Id", keyValue: 140);

            migrationBuilder.DropForeignKey(
                name: "FK_OvertimeRequests_OvertimeSlotTemplates_OvertimeSlotTemplateId",
                table: "OvertimeRequests");

            migrationBuilder.DropIndex(
                name: "IX_OvertimeRequests_OvertimeSlotTemplateId",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(name: "OvertimeSlotTemplateId", table: "OvertimeRequests");
            migrationBuilder.DropColumn(name: "OtDayHours", table: "OvertimeRequests");
            migrationBuilder.DropColumn(name: "OtNightHours", table: "OvertimeRequests");

            migrationBuilder.DropTable(name: "OvertimeSlotTemplates");
        }
    }
}
