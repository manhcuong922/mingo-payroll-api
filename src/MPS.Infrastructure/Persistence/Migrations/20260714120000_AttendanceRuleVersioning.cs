using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260714120000_AttendanceRuleVersioning")]
    public partial class AttendanceRuleVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Tạo bảng AttendanceRuleVersions.
            migrationBuilder.CreateTable(
                name: "AttendanceRuleVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttendanceRuleId = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    WorkRateNormal = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    WorkRateWeeklyRest = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    WorkRateHoliday = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRuleVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRuleVersions_AttendanceRules_AttendanceRuleId",
                        column: x => x.AttendanceRuleId,
                        principalTable: "AttendanceRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRuleVersions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRuleVersions_AttendanceRuleId_EffectiveFrom",
                table: "AttendanceRuleVersions",
                columns: new[] { "AttendanceRuleId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRuleVersions_OpenPerRule",
                table: "AttendanceRuleVersions",
                column: "AttendanceRuleId",
                unique: true,
                filter: "[EffectiveTo] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRuleVersions_CreatedByUserId",
                table: "AttendanceRuleVersions",
                column: "CreatedByUserId");

            // 2) Backfill: mỗi rule hiện tại → 1 version open bao trọn quá khứ.
            migrationBuilder.Sql(@"
INSERT INTO AttendanceRuleVersions
    (AttendanceRuleId, EffectiveFrom, EffectiveTo,
     WorkRateNormal, WorkRateWeeklyRest, WorkRateHoliday,
     CreatedAt, CreatedByUserId)
SELECT Id, '2000-01-01', NULL,
       WorkRateNormal, WorkRateWeeklyRest, WorkRateHoliday,
       COALESCE(UpdatedAt, CreatedAt),
       COALESCE(UpdatedByUserId, CreatedByUserId)
FROM   AttendanceRules;
");

            // 3) Drop 3 cột rate khỏi AttendanceRules.
            migrationBuilder.DropColumn(name: "WorkRateNormal", table: "AttendanceRules");
            migrationBuilder.DropColumn(name: "WorkRateWeeklyRest", table: "AttendanceRules");
            migrationBuilder.DropColumn(name: "WorkRateHoliday", table: "AttendanceRules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1) Add lại 3 cột với default (để nếu bảng có row, không null).
            migrationBuilder.AddColumn<decimal>(
                name: "WorkRateNormal",
                table: "AttendanceRules",
                type: "decimal(6,2)",
                nullable: false,
                defaultValue: 100.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "WorkRateWeeklyRest",
                table: "AttendanceRules",
                type: "decimal(6,2)",
                nullable: false,
                defaultValue: 200.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "WorkRateHoliday",
                table: "AttendanceRules",
                type: "decimal(6,2)",
                nullable: false,
                defaultValue: 300.00m);

            // 2) Backfill từ open version (nếu có), fallback từ version mới nhất theo EffectiveFrom.
            migrationBuilder.Sql(@"
WITH LatestVersion AS (
    SELECT AttendanceRuleId,
           WorkRateNormal, WorkRateWeeklyRest, WorkRateHoliday,
           ROW_NUMBER() OVER (
               PARTITION BY AttendanceRuleId
               ORDER BY CASE WHEN EffectiveTo IS NULL THEN 0 ELSE 1 END,
                        EffectiveFrom DESC) AS rn
    FROM AttendanceRuleVersions
)
UPDATE r
SET r.WorkRateNormal = v.WorkRateNormal,
    r.WorkRateWeeklyRest = v.WorkRateWeeklyRest,
    r.WorkRateHoliday = v.WorkRateHoliday
FROM AttendanceRules r
INNER JOIN LatestVersion v ON v.AttendanceRuleId = r.Id AND v.rn = 1;
");

            migrationBuilder.DropTable(name: "AttendanceRuleVersions");
        }
    }
}
