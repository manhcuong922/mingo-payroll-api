using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260708150000_AttendanceRecordSupportOvertime")]
    public partial class AttendanceRecordSupportOvertime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop unique index cũ (ép ShiftTemplateId NOT NULL)
            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_EmployeeInformationId_WorkDate_ShiftTemplateId",
                table: "AttendanceRecords");

            // 2. Drop FK cũ (ép IsRequired) — sẽ tạo lại nullable
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecords_ShiftTemplates_ShiftTemplateId",
                table: "AttendanceRecords");

            // 3. Alter ShiftTemplateId → nullable
            migrationBuilder.AlterColumn<int>(
                name: "ShiftTemplateId",
                table: "AttendanceRecords",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // 4. Thêm OvertimeRequestId nullable
            migrationBuilder.AddColumn<int>(
                name: "OvertimeRequestId",
                table: "AttendanceRecords",
                type: "int",
                nullable: true);

            // 5. Re-create FK ShiftTemplate (nullable)
            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_ShiftTemplates_ShiftTemplateId",
                table: "AttendanceRecords",
                column: "ShiftTemplateId",
                principalTable: "ShiftTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 6. FK OvertimeRequest
            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_OvertimeRequests_OvertimeRequestId",
                table: "AttendanceRecords",
                column: "OvertimeRequestId",
                principalTable: "OvertimeRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 7. Re-create unique index (emp, date, shift) với filter — record OT (shift=null) không đụng
            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_EmployeeInformationId_WorkDate_ShiftTemplateId",
                table: "AttendanceRecords",
                columns: new[] { "EmployeeInformationId", "WorkDate", "ShiftTemplateId" },
                unique: true,
                filter: "[ShiftTemplateId] IS NOT NULL");

            // 8. Unique index cho OvertimeRequestId — 1 OT chỉ chấm 1 lần
            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_OvertimeRequestId",
                table: "AttendanceRecords",
                column: "OvertimeRequestId",
                unique: true,
                filter: "[OvertimeRequestId] IS NOT NULL");

            // 9. Check constraint: exactly-one-of (ShiftTemplateId, OvertimeRequestId) non-null
            migrationBuilder.Sql(@"
ALTER TABLE [AttendanceRecords]
ADD CONSTRAINT [CK_AttendanceRecords_ShiftOrOvertime]
CHECK (
    ([ShiftTemplateId] IS NOT NULL AND [OvertimeRequestId] IS NULL) OR
    ([ShiftTemplateId] IS NULL     AND [OvertimeRequestId] IS NOT NULL)
);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE [AttendanceRecords] DROP CONSTRAINT [CK_AttendanceRecords_ShiftOrOvertime];");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_OvertimeRequestId",
                table: "AttendanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_EmployeeInformationId_WorkDate_ShiftTemplateId",
                table: "AttendanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecords_OvertimeRequests_OvertimeRequestId",
                table: "AttendanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecords_ShiftTemplates_ShiftTemplateId",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "OvertimeRequestId",
                table: "AttendanceRecords");

            // Rollback ShiftTemplateId → NOT NULL yêu cầu không có row nào bị NULL
            migrationBuilder.AlterColumn<int>(
                name: "ShiftTemplateId",
                table: "AttendanceRecords",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_ShiftTemplates_ShiftTemplateId",
                table: "AttendanceRecords",
                column: "ShiftTemplateId",
                principalTable: "ShiftTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_EmployeeInformationId_WorkDate_ShiftTemplateId",
                table: "AttendanceRecords",
                columns: new[] { "EmployeeInformationId", "WorkDate", "ShiftTemplateId" },
                unique: true);
        }
    }
}
