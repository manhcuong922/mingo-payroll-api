// §11 cleanup — AttendanceRule = single source of truth cho hệ số công.
//
// 1. Drop 4 column `WorkRateNormal/WeeklyRest/Holiday + IsNightShift` trên `ShiftTemplates`.
// 2. Backfill `ShiftTemplates.AppliedAttendanceRuleId` NULL → rule "Default Office"
//    (tự tạo nếu DB chưa có AttendanceRule nào).
// 3. ALTER `AppliedAttendanceRuleId` từ nullable → NOT NULL.
//
// Snapshot có pre-existing issue (xem 20260625000000_CompanyPolicyModule note) nên
// migration này viết tay. Đã build clean qua `dotnet build`.

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Attendance_RuleSourceOfTruth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Drop foreign key + index (nullable) ───────────────────────
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftTemplates_AttendanceRules_AppliedAttendanceRuleId",
                table: "ShiftTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ShiftTemplates_AppliedAttendanceRuleId",
                table: "ShiftTemplates");

            // ── 2. Backfill: tạo rule mặc định nếu chưa có, link ShiftTemplate NULL ──
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [AttendanceRules] WHERE [Name] = N'Default Office')
                BEGIN
                    -- Cần 1 user để gán CreatedByUserId; ưu tiên SystemAdmin (RoleId=1), fallback admin user nhỏ nhất Id.
                    DECLARE @AdminId int = (
                        SELECT TOP 1 [Id] FROM [Users] WHERE [RoleId] = 1 ORDER BY [Id]
                    );
                    IF @AdminId IS NULL
                        SET @AdminId = (SELECT TOP 1 [Id] FROM [Users] ORDER BY [Id]);

                    IF @AdminId IS NOT NULL
                    BEGIN
                        INSERT INTO [AttendanceRules]
                            ([Name], [Description], [WorkRateNormal], [WorkRateWeeklyRest], [WorkRateHoliday],
                             [IsActive], [CreatedAt], [CreatedByUserId])
                        VALUES
                            (N'Default Office', N'Quy chế mặc định: 100% ngày thường, 200% nghỉ tuần, 300% lễ (Điều 98 BLLĐ).',
                             1.00, 2.00, 3.00, 1, SYSUTCDATETIME(), @AdminId);
                    END
                END
            ");

            migrationBuilder.Sql(@"
                DECLARE @DefaultRuleId int = (SELECT TOP 1 [Id] FROM [AttendanceRules] WHERE [Name] = N'Default Office');
                IF @DefaultRuleId IS NULL
                    SET @DefaultRuleId = (SELECT TOP 1 [Id] FROM [AttendanceRules] ORDER BY [Id]);

                IF @DefaultRuleId IS NOT NULL
                BEGIN
                    UPDATE [ShiftTemplates]
                    SET [AppliedAttendanceRuleId] = @DefaultRuleId
                    WHERE [AppliedAttendanceRuleId] IS NULL;
                END
            ");

            // ── 3. Drop dead columns ─────────────────────────────────────────
            migrationBuilder.DropColumn(name: "WorkRateNormal",     table: "ShiftTemplates");
            migrationBuilder.DropColumn(name: "WorkRateWeeklyRest", table: "ShiftTemplates");
            migrationBuilder.DropColumn(name: "WorkRateHoliday",    table: "ShiftTemplates");
            migrationBuilder.DropColumn(name: "IsNightShift",       table: "ShiftTemplates");

            // ── 4. ALTER FK NOT NULL + re-add FK constraint ──────────────────
            migrationBuilder.AlterColumn<int>(
                name: "AppliedAttendanceRuleId",
                table: "ShiftTemplates",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTemplates_AppliedAttendanceRuleId",
                table: "ShiftTemplates",
                column: "AppliedAttendanceRuleId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftTemplates_AttendanceRules_AppliedAttendanceRuleId",
                table: "ShiftTemplates",
                column: "AppliedAttendanceRuleId",
                principalTable: "AttendanceRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftTemplates_AttendanceRules_AppliedAttendanceRuleId",
                table: "ShiftTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ShiftTemplates_AppliedAttendanceRuleId",
                table: "ShiftTemplates");

            migrationBuilder.AlterColumn<int>(
                name: "AppliedAttendanceRuleId",
                table: "ShiftTemplates",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: false);

            migrationBuilder.AddColumn<decimal>(
                name: "WorkRateNormal",
                table: "ShiftTemplates",
                type: "decimal(4,2)",
                nullable: false,
                defaultValue: 1.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "WorkRateWeeklyRest",
                table: "ShiftTemplates",
                type: "decimal(4,2)",
                nullable: false,
                defaultValue: 2.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "WorkRateHoliday",
                table: "ShiftTemplates",
                type: "decimal(4,2)",
                nullable: false,
                defaultValue: 3.00m);

            migrationBuilder.AddColumn<bool>(
                name: "IsNightShift",
                table: "ShiftTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Re-seed copy từ rule cho data đã có.
            migrationBuilder.Sql(@"
                UPDATE st
                SET st.[WorkRateNormal]     = ar.[WorkRateNormal],
                    st.[WorkRateWeeklyRest] = ar.[WorkRateWeeklyRest],
                    st.[WorkRateHoliday]    = ar.[WorkRateHoliday]
                FROM [ShiftTemplates] st
                INNER JOIN [AttendanceRules] ar ON st.[AppliedAttendanceRuleId] = ar.[Id];
            ");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTemplates_AppliedAttendanceRuleId",
                table: "ShiftTemplates",
                column: "AppliedAttendanceRuleId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftTemplates_AttendanceRules_AppliedAttendanceRuleId",
                table: "ShiftTemplates",
                column: "AppliedAttendanceRuleId",
                principalTable: "AttendanceRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
