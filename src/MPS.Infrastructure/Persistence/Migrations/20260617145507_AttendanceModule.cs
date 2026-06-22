using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WorkRateNormal = table.Column<decimal>(type: "decimal(4,2)", nullable: false, defaultValue: 1.00m),
                    WorkRateWeeklyRest = table.Column<decimal>(type: "decimal(4,2)", nullable: false, defaultValue: 2.00m),
                    WorkRateHoliday = table.Column<decimal>(type: "decimal(4,2)", nullable: false, defaultValue: 3.00m),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRules_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRules_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    PaidRate = table.Column<decimal>(type: "decimal(4,2)", nullable: true),
                    MaxDaysPerRequest = table.Column<decimal>(type: "decimal(5,1)", nullable: true),
                    RequireDocument = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeductsBalance = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveTypes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveTypes_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OvertimeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeInformationId = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OtStartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    OtEndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    OtHours = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OvertimeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OvertimeRequests_EmployeeInformations_EmployeeInformationId",
                        column: x => x.EmployeeInformationId,
                        principalTable: "EmployeeInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OvertimeRequests_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OvertimeRequests_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OvertimeRequests_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PublicHolidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HolidayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Category = table.Column<byte>(type: "tinyint", nullable: false),
                    PayRateOverride = table.Column<decimal>(type: "decimal(4,2)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicHolidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicHolidays_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicHolidays_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    BreakStartTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    BreakEndTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    StandardHours = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    StandardDays = table.Column<decimal>(type: "decimal(3,2)", nullable: false, defaultValue: 1.00m),
                    WorkRateNormal = table.Column<decimal>(type: "decimal(4,2)", nullable: false, defaultValue: 1.00m),
                    WorkRateWeeklyRest = table.Column<decimal>(type: "decimal(4,2)", nullable: false, defaultValue: 2.00m),
                    WorkRateHoliday = table.Column<decimal>(type: "decimal(4,2)", nullable: false, defaultValue: 3.00m),
                    IsNightShift = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AppliedAttendanceRuleId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftTemplates_AttendanceRules_AppliedAttendanceRuleId",
                        column: x => x.AppliedAttendanceRuleId,
                        principalTable: "AttendanceRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftTemplates_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftTemplates_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeInformationId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "int", nullable: false),
                    BaseDays = table.Column<decimal>(type: "decimal(4,1)", nullable: false, defaultValue: 12m),
                    SeniorityBonusDays = table.Column<decimal>(type: "decimal(4,1)", nullable: false, defaultValue: 0m),
                    TotalDays = table.Column<decimal>(type: "decimal(4,1)", nullable: false),
                    UsedDays = table.Column<decimal>(type: "decimal(4,1)", nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveBalances_EmployeeInformations_EmployeeInformationId",
                        column: x => x.EmployeeInformationId,
                        principalTable: "EmployeeInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveBalances_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveBalances_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveBalances_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeInformationId = table.Column<int>(type: "int", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalDays = table.Column<decimal>(type: "decimal(4,1)", nullable: false),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SupportingDocumentUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_EmployeeInformations_EmployeeInformationId",
                        column: x => x.EmployeeInformationId,
                        principalTable: "EmployeeInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeInformationId = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftTemplateId = table.Column<int>(type: "int", nullable: false),
                    CheckInTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    CheckOutTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OverrideReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_EmployeeInformations_EmployeeInformationId",
                        column: x => x.EmployeeInformationId,
                        principalTable: "EmployeeInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_ShiftTemplates_ShiftTemplateId",
                        column: x => x.ShiftTemplateId,
                        principalTable: "ShiftTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ShiftTemplateId = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false),
                    RepeatUnit = table.Column<byte>(type: "tinyint", nullable: false),
                    RepeatEvery = table.Column<int>(type: "int", nullable: false),
                    DaysOfWeekMask = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DaysOfMonthMask = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleTemplates_ShiftTemplates_ShiftTemplateId",
                        column: x => x.ShiftTemplateId,
                        principalTable: "ShiftTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleTemplates_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleTemplates_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttendanceRecordId = table.Column<int>(type: "int", nullable: false),
                    EmployeeInformationId = table.Column<int>(type: "int", nullable: false),
                    NewStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    NewCheckInTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    NewCheckOutTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OriginalStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    OriginalCheckInTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    OriginalCheckOutTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceAdjustments_AttendanceRecords_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalTable: "AttendanceRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceAdjustments_EmployeeInformations_EmployeeInformationId",
                        column: x => x.EmployeeInformationId,
                        principalTable: "EmployeeInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceAdjustments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceAdjustments_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceAdjustments_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeShiftAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeInformationId = table.Column<int>(type: "int", nullable: false),
                    ShiftTemplateId = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ScheduleTemplateId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeShiftAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeShiftAssignments_EmployeeInformations_EmployeeInformationId",
                        column: x => x.EmployeeInformationId,
                        principalTable: "EmployeeInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeShiftAssignments_ScheduleTemplates_ScheduleTemplateId",
                        column: x => x.ScheduleTemplateId,
                        principalTable: "ScheduleTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EmployeeShiftAssignments_ShiftTemplates_ShiftTemplateId",
                        column: x => x.ShiftTemplateId,
                        principalTable: "ShiftTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeShiftAssignments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleTemplateEmployees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduleTemplateId = table.Column<int>(type: "int", nullable: false),
                    EmployeeInformationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleTemplateEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleTemplateEmployees_EmployeeInformations_EmployeeInformationId",
                        column: x => x.EmployeeInformationId,
                        principalTable: "EmployeeInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleTemplateEmployees_ScheduleTemplates_ScheduleTemplateId",
                        column: x => x.ScheduleTemplateId,
                        principalTable: "ScheduleTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustments_AttendanceRecordId",
                table: "AttendanceAdjustments",
                column: "AttendanceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustments_CreatedByUserId",
                table: "AttendanceAdjustments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustments_EmployeeInformationId_Status",
                table: "AttendanceAdjustments",
                columns: new[] { "EmployeeInformationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustments_ReviewedByUserId",
                table: "AttendanceAdjustments",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustments_UpdatedByUserId",
                table: "AttendanceAdjustments",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_CreatedByUserId",
                table: "AttendanceRecords",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_EmployeeInformationId_WorkDate_ShiftTemplateId",
                table: "AttendanceRecords",
                columns: new[] { "EmployeeInformationId", "WorkDate", "ShiftTemplateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_ShiftTemplateId",
                table: "AttendanceRecords",
                column: "ShiftTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_UpdatedByUserId",
                table: "AttendanceRecords",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_WorkDate",
                table: "AttendanceRecords",
                column: "WorkDate");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRules_CreatedByUserId",
                table: "AttendanceRules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRules_Name",
                table: "AttendanceRules",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRules_UpdatedByUserId",
                table: "AttendanceRules",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftAssignments_CreatedByUserId",
                table: "EmployeeShiftAssignments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftAssignments_EmployeeInformationId_WorkDate_ShiftTemplateId",
                table: "EmployeeShiftAssignments",
                columns: new[] { "EmployeeInformationId", "WorkDate", "ShiftTemplateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftAssignments_ScheduleTemplateId",
                table: "EmployeeShiftAssignments",
                column: "ScheduleTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftAssignments_ShiftTemplateId",
                table: "EmployeeShiftAssignments",
                column: "ShiftTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftAssignments_WorkDate",
                table: "EmployeeShiftAssignments",
                column: "WorkDate");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_CreatedByUserId",
                table: "LeaveBalances",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_EmployeeInformationId_Year_LeaveTypeId",
                table: "LeaveBalances",
                columns: new[] { "EmployeeInformationId", "Year", "LeaveTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_LeaveTypeId",
                table: "LeaveBalances",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_UpdatedByUserId",
                table: "LeaveBalances",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_CreatedByUserId",
                table: "LeaveRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_EmployeeInformationId_Status",
                table: "LeaveRequests",
                columns: new[] { "EmployeeInformationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_LeaveTypeId",
                table: "LeaveRequests",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_ReviewedByUserId",
                table: "LeaveRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_StartDate_EndDate",
                table: "LeaveRequests",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_UpdatedByUserId",
                table: "LeaveRequests",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTypes_Code",
                table: "LeaveTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTypes_CreatedByUserId",
                table: "LeaveTypes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTypes_Name",
                table: "LeaveTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTypes_UpdatedByUserId",
                table: "LeaveTypes",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_CreatedByUserId",
                table: "OvertimeRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_EmployeeInformationId_WorkDate_Status",
                table: "OvertimeRequests",
                columns: new[] { "EmployeeInformationId", "WorkDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_ReviewedByUserId",
                table: "OvertimeRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_UpdatedByUserId",
                table: "OvertimeRequests",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicHolidays_CreatedByUserId",
                table: "PublicHolidays",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicHolidays_HolidayDate",
                table: "PublicHolidays",
                column: "HolidayDate",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PublicHolidays_UpdatedByUserId",
                table: "PublicHolidays",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTemplateEmployees_EmployeeInformationId",
                table: "ScheduleTemplateEmployees",
                column: "EmployeeInformationId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTemplateEmployees_ScheduleTemplateId_EmployeeInformationId",
                table: "ScheduleTemplateEmployees",
                columns: new[] { "ScheduleTemplateId", "EmployeeInformationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTemplates_CreatedByUserId",
                table: "ScheduleTemplates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTemplates_Name",
                table: "ScheduleTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTemplates_ShiftTemplateId",
                table: "ScheduleTemplates",
                column: "ShiftTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTemplates_UpdatedByUserId",
                table: "ScheduleTemplates",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTemplates_AppliedAttendanceRuleId",
                table: "ShiftTemplates",
                column: "AppliedAttendanceRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTemplates_Code",
                table: "ShiftTemplates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTemplates_CreatedByUserId",
                table: "ShiftTemplates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTemplates_Name",
                table: "ShiftTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTemplates_UpdatedByUserId",
                table: "ShiftTemplates",
                column: "UpdatedByUserId");

            // ===== Seed permissions (IDs 89-123) — Attendance module + LeaveType =====
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[,]
                {
                    {  89, "Read",    "ShiftTemplate.Read.All",          "Xem ca làm việc mẫu.",                    "ShiftTemplate",        "All"  },
                    {  90, "Create",  "ShiftTemplate.Create.All",        "Tạo ca làm việc mẫu mới.",                "ShiftTemplate",        "All"  },
                    {  91, "Update",  "ShiftTemplate.Update.All",        "Cập nhật ca làm việc mẫu.",               "ShiftTemplate",        "All"  },
                    {  92, "Delete",  "ShiftTemplate.Delete.All",        "Xóa hoặc ngưng kích hoạt ca mẫu.",        "ShiftTemplate",        "All"  },

                    {  93, "Read",    "EmployeeShift.Read.All",          "Xem phân ca nhân viên.",                   "EmployeeShift",        "All"  },
                    {  94, "Create",  "EmployeeShift.Create.All",        "Phân ca cho nhân viên.",                   "EmployeeShift",        "All"  },
                    {  95, "Delete",  "EmployeeShift.Delete.All",        "Hủy phân ca nhân viên.",                   "EmployeeShift",        "All"  },

                    {  96, "Read",    "PublicHoliday.Read.All",          "Xem danh sách ngày lễ.",                   "PublicHoliday",        "All"  },
                    {  97, "Create",  "PublicHoliday.Create.All",        "Tạo ngày lễ có hưởng lương.",              "PublicHoliday",        "All"  },
                    {  98, "Update",  "PublicHoliday.Update.All",        "Cập nhật ngày lễ.",                        "PublicHoliday",        "All"  },
                    {  99, "Delete",  "PublicHoliday.Delete.All",        "Xóa ngày lễ.",                             "PublicHoliday",        "All"  },

                    { 100, "Read",    "Attendance.Read.All",             "Xem chấm công của mọi nhân viên.",         "Attendance",           "All"  },
                    { 101, "Read",    "Attendance.Read.Self",            "Xem chấm công của bản thân.",              "Attendance",           "Self" },
                    { 102, "Create",  "Attendance.Create.All",           "Ghi nhận chấm công.",                      "Attendance",           "All"  },
                    { 103, "Update",  "Attendance.Update.All",           "Cập nhật chấm công.",                      "Attendance",           "All"  },

                    { 104, "Read",    "OvertimeRequest.Read.All",        "Xem yêu cầu tăng ca của mọi nhân viên.",   "OvertimeRequest",      "All"  },
                    { 105, "Read",    "OvertimeRequest.Read.Self",       "Xem yêu cầu tăng ca của bản thân.",        "OvertimeRequest",      "Self" },
                    { 106, "Create",  "OvertimeRequest.Create.Self",     "Tạo yêu cầu tăng ca cho bản thân.",        "OvertimeRequest",      "Self" },
                    { 107, "Approve", "OvertimeRequest.Approve.All",     "Duyệt yêu cầu tăng ca.",                   "OvertimeRequest",      "All"  },

                    { 108, "Read",    "LeaveRequest.Read.All",           "Xem yêu cầu nghỉ phép của mọi nhân viên.", "LeaveRequest",         "All"  },
                    { 109, "Read",    "LeaveRequest.Read.Self",          "Xem yêu cầu nghỉ phép của bản thân.",      "LeaveRequest",         "Self" },
                    { 110, "Create",  "LeaveRequest.Create.Self",        "Tạo yêu cầu nghỉ phép cho bản thân.",      "LeaveRequest",         "Self" },
                    { 111, "Approve", "LeaveRequest.Approve.All",        "Duyệt yêu cầu nghỉ phép.",                 "LeaveRequest",         "All"  },

                    { 112, "Read",    "AttendanceAdjustment.Read.All",   "Xem yêu cầu điều chỉnh chấm công.",        "AttendanceAdjustment", "All"  },
                    { 113, "Create",  "AttendanceAdjustment.Create.Self","Tạo yêu cầu điều chỉnh chấm công.",        "AttendanceAdjustment", "Self" },
                    { 114, "Approve", "AttendanceAdjustment.Approve.All","Duyệt yêu cầu điều chỉnh.",                "AttendanceAdjustment", "All"  },
                    { 115, "Update",  "AttendanceAdjustment.Override.All","HR ghi đè chấm công không qua workflow.", "AttendanceAdjustment", "All"  },

                    { 116, "Read",    "AttendanceRule.Read.All",         "Xem preset rule chấm công.",               "AttendanceRule",       "All"  },
                    { 117, "Create",  "AttendanceRule.Create.All",       "Tạo preset rule chấm công.",               "AttendanceRule",       "All"  },
                    { 118, "Update",  "AttendanceRule.Update.All",       "Cập nhật preset rule chấm công.",          "AttendanceRule",       "All"  },
                    { 119, "Delete",  "AttendanceRule.Delete.All",       "Xóa preset rule chấm công.",               "AttendanceRule",       "All"  },

                    { 120, "Read",    "LeaveType.Read.All",              "Xem cấu hình loại nghỉ phép.",             "LeaveType",            "All"  },
                    { 121, "Create",  "LeaveType.Create.All",            "Tạo loại nghỉ phép.",                      "LeaveType",            "All"  },
                    { 122, "Update",  "LeaveType.Update.All",            "Cập nhật loại nghỉ phép.",                 "LeaveType",            "All"  },
                    { 123, "Delete",  "LeaveType.Delete.All",            "Xóa/Deactivate loại nghỉ phép.",           "LeaveType",            "All"  }
                });

            // ===== Role permissions seed =====
            // SystemAdmin (1) + HR (2): full admin perms (no Self perms)
            // Accountant (3): PublicHoliday.Read, Attendance.Read.All
            // Director (4): read-only across module
            // DepartmentManager (5): operational management
            // Employee (7): self-service perms
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    // ── SystemAdmin (1) ─────────────────────────────────────────
                    {  89, 1 }, {  90, 1 }, {  91, 1 }, {  92, 1 },
                    {  93, 1 }, {  94, 1 }, {  95, 1 },
                    {  96, 1 }, {  97, 1 }, {  98, 1 }, {  99, 1 },
                    { 100, 1 }, { 102, 1 }, { 103, 1 },
                    { 104, 1 }, { 107, 1 },
                    { 108, 1 }, { 111, 1 },
                    { 112, 1 }, { 114, 1 }, { 115, 1 },
                    { 116, 1 }, { 117, 1 }, { 118, 1 }, { 119, 1 },
                    { 120, 1 }, { 121, 1 }, { 122, 1 }, { 123, 1 },

                    // ── HR (2) ─────────────────────────────────────────────────
                    {  89, 2 }, {  90, 2 }, {  91, 2 }, {  92, 2 },
                    {  93, 2 }, {  94, 2 }, {  95, 2 },
                    {  96, 2 }, {  97, 2 }, {  98, 2 }, {  99, 2 },
                    { 100, 2 }, { 102, 2 }, { 103, 2 },
                    { 104, 2 }, { 107, 2 },
                    { 108, 2 }, { 111, 2 },
                    { 112, 2 }, { 114, 2 }, { 115, 2 },
                    { 116, 2 }, { 117, 2 }, { 118, 2 }, { 119, 2 },
                    { 120, 2 }, { 121, 2 }, { 122, 2 }, { 123, 2 },

                    // ── Accountant (3) — read PublicHoliday + Attendance ───────
                    {  96, 3 }, { 100, 3 },

                    // ── Director (4) — read-only ──────────────────────────────
                    {  89, 4 }, {  93, 4 }, {  96, 4 }, { 100, 4 }, { 104, 4 }, { 108, 4 },
                    { 116, 4 }, { 120, 4 },

                    // ── DepartmentManager (5) — operational manager ───────────
                    {  89, 5 }, {  90, 5 }, {  91, 5 }, {  92, 5 },
                    {  93, 5 }, {  94, 5 }, {  95, 5 },
                    {  96, 5 },
                    { 100, 5 }, { 102, 5 }, { 103, 5 },
                    { 104, 5 }, { 107, 5 },
                    { 108, 5 }, { 111, 5 },
                    { 112, 5 }, { 114, 5 },
                    { 116, 5 }, { 117, 5 }, { 118, 5 }, { 119, 5 },
                    { 120, 5 },

                    // ── Employee (7) — self-service ───────────────────────────
                    {  96, 7 }, { 101, 7 },
                    { 105, 7 }, { 106, 7 },
                    { 109, 7 }, { 110, 7 },
                    { 113, 7 },
                    { 120, 7 }
                });

            // AttendanceRules + LeaveTypes seed nằm trong DatabaseSeeder runtime
            // (cả 2 bảng có FK → Users.Id mà admin user chỉ tồn tại sau khi seeder chạy).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM RolePermissions WHERE PermissionId BETWEEN 89 AND 123;");
            migrationBuilder.Sql("DELETE FROM Permissions WHERE Id BETWEEN 89 AND 123;");


            migrationBuilder.DropTable(
                name: "AttendanceAdjustments");

            migrationBuilder.DropTable(
                name: "EmployeeShiftAssignments");

            migrationBuilder.DropTable(
                name: "LeaveBalances");

            migrationBuilder.DropTable(
                name: "LeaveRequests");

            migrationBuilder.DropTable(
                name: "OvertimeRequests");

            migrationBuilder.DropTable(
                name: "PublicHolidays");

            migrationBuilder.DropTable(
                name: "ScheduleTemplateEmployees");

            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "LeaveTypes");

            migrationBuilder.DropTable(
                name: "ScheduleTemplates");

            migrationBuilder.DropTable(
                name: "ShiftTemplates");

            migrationBuilder.DropTable(
                name: "AttendanceRules");
        }
    }
}
