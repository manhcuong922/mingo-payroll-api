using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Phase 1 refactor OT multiplier:
    ///   1. Đổi đơn vị WorkRate* (LaborLawConstants + AttendanceRule) sang percent (%).
    ///   2. Bỏ module OvertimeSlotTemplate — mọi số lấy từ LaborLawConstants + công thức.
    ///   3. Snapshot LaborLawConstantsVersionId trên OvertimeRequest — khoá rate tại thời điểm tạo.
    ///
    /// Chi tiết: docs/plans/overtime-request-rate-window-refactor-plan.md.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260710030000_OvertimeMultiplierRefactorPhase1")]
    public partial class OvertimeMultiplierRefactorPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. AlterColumn decimal(4,2) → decimal(6,2) TRƯỚC ──
            // Phải nới precision trước, nếu không UPDATE ×100 sẽ overflow (3.00 * 100 = 300, không fit decimal(4,2)).
            migrationBuilder.AlterColumn<decimal>(
                name: "WorkRateNormal", table: "AttendanceRules",
                type: "decimal(6,2)", nullable: false, defaultValue: 100.00m,
                oldClrType: typeof(decimal), oldType: "decimal(4,2)", oldDefaultValue: 1.00m);
            migrationBuilder.AlterColumn<decimal>(
                name: "WorkRateWeeklyRest", table: "AttendanceRules",
                type: "decimal(6,2)", nullable: false, defaultValue: 200.00m,
                oldClrType: typeof(decimal), oldType: "decimal(4,2)", oldDefaultValue: 2.00m);
            migrationBuilder.AlterColumn<decimal>(
                name: "WorkRateHoliday", table: "AttendanceRules",
                type: "decimal(6,2)", nullable: false, defaultValue: 300.00m,
                oldClrType: typeof(decimal), oldType: "decimal(4,2)", oldDefaultValue: 3.00m);

            migrationBuilder.AlterColumn<decimal>(
                name: "WorkRateNormalFloor", table: "LaborLawConstantsVersions",
                type: "decimal(6,2)", nullable: false,
                oldClrType: typeof(decimal), oldType: "decimal(4,2)");
            migrationBuilder.AlterColumn<decimal>(
                name: "WorkRateWeeklyRestFloor", table: "LaborLawConstantsVersions",
                type: "decimal(6,2)", nullable: false,
                oldClrType: typeof(decimal), oldType: "decimal(4,2)");
            migrationBuilder.AlterColumn<decimal>(
                name: "WorkRateHolidayFloor", table: "LaborLawConstantsVersions",
                type: "decimal(6,2)", nullable: false,
                oldClrType: typeof(decimal), oldType: "decimal(4,2)");

            // ── 2. UPDATE data ×100 SAU khi column đã đủ precision ──
            // Guard "<= 10" để chạy lại lần 2 không nhân nữa (idempotent với data đã ở percent-form).
            migrationBuilder.Sql(
                "UPDATE [AttendanceRules] SET " +
                "  [WorkRateNormal] = [WorkRateNormal] * 100, " +
                "  [WorkRateWeeklyRest] = [WorkRateWeeklyRest] * 100, " +
                "  [WorkRateHoliday] = [WorkRateHoliday] * 100 " +
                "WHERE [WorkRateNormal] <= 10;");
            migrationBuilder.Sql(
                "UPDATE [LaborLawConstantsVersions] SET " +
                "  [WorkRateNormalFloor] = [WorkRateNormalFloor] * 100, " +
                "  [WorkRateWeeklyRestFloor] = [WorkRateWeeklyRestFloor] * 100, " +
                "  [WorkRateHolidayFloor] = [WorkRateHolidayFloor] * 100 " +
                "WHERE [WorkRateNormalFloor] <= 10;");

            // ── 3. Rename OvertimeSlotTemplateId → LaborLawConstantsVersionId + FK mới ──
            // Data cũ (nếu có FK slot) sẽ bị NULL vì FK không còn valid — chấp nhận vì slot cũ đâu còn.
            migrationBuilder.DropForeignKey(
                name: "FK_OvertimeRequests_OvertimeSlotTemplates_OvertimeSlotTemplateId",
                table: "OvertimeRequests");
            migrationBuilder.DropIndex(
                name: "IX_OvertimeRequests_OvertimeSlotTemplateId",
                table: "OvertimeRequests");
            migrationBuilder.Sql("UPDATE [OvertimeRequests] SET [OvertimeSlotTemplateId] = NULL;");
            migrationBuilder.RenameColumn(
                name: "OvertimeSlotTemplateId",
                table: "OvertimeRequests",
                newName: "LaborLawConstantsVersionId");

            // Backfill: cho mỗi OvertimeRequest, snapshot version active tại WorkDate.
            migrationBuilder.Sql(@"
                UPDATE ot
                SET ot.[LaborLawConstantsVersionId] = (
                    SELECT TOP 1 v.[Id]
                    FROM [LaborLawConstantsVersions] v
                    WHERE v.[ValidFrom] <= ot.[WorkDate] AND v.[IsArchived] = 0
                    ORDER BY v.[ValidFrom] DESC
                )
                FROM [OvertimeRequests] ot;");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_LaborLawConstantsVersionId",
                table: "OvertimeRequests",
                column: "LaborLawConstantsVersionId");
            migrationBuilder.AddForeignKey(
                name: "FK_OvertimeRequests_LaborLawConstantsVersions_LaborLawConstantsVersionId",
                table: "OvertimeRequests",
                column: "LaborLawConstantsVersionId",
                principalTable: "LaborLawConstantsVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ── 4. Drop OvertimeSlotTemplates ──
            migrationBuilder.DropTable(name: "OvertimeSlotTemplates");

            // ── 5. Xoá permission 137-140 (OvertimeSlotTemplate) ──
            foreach (var permId in new[] { 137, 138, 139, 140 })
            {
                migrationBuilder.Sql(
                    $"DELETE FROM [RolePermissions] WHERE [PermissionId] = {permId};");
                migrationBuilder.Sql(
                    $"DELETE FROM [Permissions] WHERE [Id] = {permId};");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── 1. Recreate Permission 137-140 + grants ──
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[,]
                {
                    { 137, "Read",   "OvertimeSlotTemplate.Read.All",   "Xem danh sách khung giờ làm thêm có sẵn.", "OvertimeSlotTemplate", "All" },
                    { 138, "Create", "OvertimeSlotTemplate.Create.All", "Tạo khung giờ làm thêm mới.",              "OvertimeSlotTemplate", "All" },
                    { 139, "Update", "OvertimeSlotTemplate.Update.All", "Sửa khung giờ làm thêm.",                  "OvertimeSlotTemplate", "All" },
                    { 140, "Delete", "OvertimeSlotTemplate.Delete.All", "Xoá hoặc deactivate khung giờ.",           "OvertimeSlotTemplate", "All" },
                });
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 137, 1 }, { 137, 2 }, { 137, 3 }, { 137, 4 }, { 137, 5 }, { 137, 6 }, { 137, 7 },
                    { 138, 1 }, { 138, 2 }, { 138, 5 },
                    { 139, 1 }, { 139, 2 }, { 139, 5 },
                    { 140, 1 }, { 140, 2 }, { 140, 5 },
                });

            // ── 2. Recreate OvertimeSlotTemplates (empty) ──
            migrationBuilder.CreateTable(
                name: "OvertimeSlotTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartTime = table.Column<System.TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<System.TimeOnly>(type: "time", nullable: false),
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
                    CreatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: true),
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
            migrationBuilder.CreateIndex(name: "IX_OvertimeSlotTemplates_Code", table: "OvertimeSlotTemplates", column: "Code", unique: true);
            migrationBuilder.CreateIndex(name: "IX_OvertimeSlotTemplates_IsActive", table: "OvertimeSlotTemplates", column: "IsActive");
            migrationBuilder.CreateIndex(name: "IX_OvertimeSlotTemplates_CreatedByUserId", table: "OvertimeSlotTemplates", column: "CreatedByUserId");
            migrationBuilder.CreateIndex(name: "IX_OvertimeSlotTemplates_UpdatedByUserId", table: "OvertimeSlotTemplates", column: "UpdatedByUserId");

            // ── 3. Revert OvertimeRequests FK ──
            migrationBuilder.DropForeignKey(
                name: "FK_OvertimeRequests_LaborLawConstantsVersions_LaborLawConstantsVersionId",
                table: "OvertimeRequests");
            migrationBuilder.DropIndex(
                name: "IX_OvertimeRequests_LaborLawConstantsVersionId",
                table: "OvertimeRequests");
            migrationBuilder.Sql("UPDATE [OvertimeRequests] SET [LaborLawConstantsVersionId] = NULL;");
            migrationBuilder.RenameColumn(
                name: "LaborLawConstantsVersionId",
                table: "OvertimeRequests",
                newName: "OvertimeSlotTemplateId");
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

            // ── 4. Revert WorkRate* ÷100 rồi AlterColumn về decimal(4,2) ──
            migrationBuilder.Sql(
                "UPDATE [LaborLawConstantsVersions] SET " +
                "  [WorkRateNormalFloor] = [WorkRateNormalFloor] / 100, " +
                "  [WorkRateWeeklyRestFloor] = [WorkRateWeeklyRestFloor] / 100, " +
                "  [WorkRateHolidayFloor] = [WorkRateHolidayFloor] / 100 " +
                "WHERE [WorkRateNormalFloor] > 10;");
            migrationBuilder.Sql(
                "UPDATE [AttendanceRules] SET " +
                "  [WorkRateNormal] = [WorkRateNormal] / 100, " +
                "  [WorkRateWeeklyRest] = [WorkRateWeeklyRest] / 100, " +
                "  [WorkRateHoliday] = [WorkRateHoliday] / 100 " +
                "WHERE [WorkRateNormal] > 10;");

            migrationBuilder.AlterColumn<decimal>(
                name: "WorkRateNormalFloor", table: "LaborLawConstantsVersions",
                type: "decimal(4,2)", nullable: false,
                oldClrType: typeof(decimal), oldType: "decimal(6,2)");
            migrationBuilder.AlterColumn<decimal>(
                name: "WorkRateWeeklyRestFloor", table: "LaborLawConstantsVersions",
                type: "decimal(4,2)", nullable: false,
                oldClrType: typeof(decimal), oldType: "decimal(6,2)");
            migrationBuilder.AlterColumn<decimal>(
                name: "WorkRateHolidayFloor", table: "LaborLawConstantsVersions",
                type: "decimal(4,2)", nullable: false,
                oldClrType: typeof(decimal), oldType: "decimal(6,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "WorkRateNormal", table: "AttendanceRules",
                type: "decimal(4,2)", nullable: false, defaultValue: 1.00m,
                oldClrType: typeof(decimal), oldType: "decimal(6,2)", oldDefaultValue: 100.00m);
            migrationBuilder.AlterColumn<decimal>(
                name: "WorkRateWeeklyRest", table: "AttendanceRules",
                type: "decimal(4,2)", nullable: false, defaultValue: 2.00m,
                oldClrType: typeof(decimal), oldType: "decimal(6,2)", oldDefaultValue: 200.00m);
            migrationBuilder.AlterColumn<decimal>(
                name: "WorkRateHoliday", table: "AttendanceRules",
                type: "decimal(4,2)", nullable: false, defaultValue: 3.00m,
                oldClrType: typeof(decimal), oldType: "decimal(6,2)", oldDefaultValue: 300.00m);
        }
    }
}
