using MPS.Domain.Enums.Attendance;

namespace MPS.Application.DTOs.Attendance;

public sealed record ShiftTemplateDto(
    int Id,
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeOnly? BreakStartTime,
    TimeOnly? BreakEndTime,
    decimal StandardHours,
    decimal StandardDays,
    decimal WorkRateNormal,
    decimal WorkRateWeeklyRest,
    decimal WorkRateHoliday,
    bool IsNightShift,
    int? AppliedAttendanceRuleId,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AttendanceRuleDto(
    int Id,
    string Name,
    string? Description,
    decimal WorkRateNormal,
    decimal WorkRateWeeklyRest,
    decimal WorkRateHoliday,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<int>? UsedByShiftTemplateIds = null);

public sealed record EmployeeShiftAssignmentDto(
    int Id,
    int EmployeeInformationId,
    string EmployeeFullName,
    int ShiftTemplateId,
    string ShiftTemplateName,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly WorkDate,
    DateTime CreatedAt);

public sealed record ShiftRosterLockedDateDto(
    int EmployeeInformationId,
    DateOnly WorkDate,
    int ShiftTemplateId);

public sealed record ShiftRosterDto(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<int> EmployeeInformationIds,
    IReadOnlyList<EmployeeShiftAssignmentDto> Assignments,
    IReadOnlyList<ShiftRosterLockedDateDto> LockedDates);

public sealed record ScheduleTemplateEmployeeDto(
    int EmployeeInformationId,
    string EmployeeFullName,
    string? EmployeeCode);

public sealed record ScheduleTemplateDto(
    int Id,
    string Name,
    int ShiftTemplateId,
    string ShiftTemplateCode,
    string ShiftTemplateName,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo,
    ScheduleRepeatUnit RepeatUnit,
    int RepeatEvery,
    IReadOnlyList<int> DaysOfWeek,
    IReadOnlyList<int> DaysOfMonth,
    bool IsActive,
    string? Notes,
    IReadOnlyList<ScheduleTemplateEmployeeDto> Employees,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record ScheduleTemplateApplyConflictDto(
    int EmployeeInformationId,
    DateOnly WorkDate,
    MPS.Application.Features.Attendance.EmployeeShift.Commands.BatchShiftItemStatus Status,
    string? Reason);

public sealed record ScheduleTemplateApplyResultDto(
    int ScheduleTemplateId,
    int ExpandedDateCount,
    int Applied,
    int Skipped,
    int Conflicts,
    IReadOnlyList<ScheduleTemplateApplyConflictDto> ConflictItems);

public sealed record PublicHolidayDto(
    int Id,
    string Name,
    DateOnly HolidayDate,
    PublicHolidayCategory Category,
    decimal? PayRateOverride,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AttendanceRecordDto(
    int Id,
    int EmployeeInformationId,
    string EmployeeFullName,
    DateOnly WorkDate,
    int? ShiftTemplateId,
    string? ShiftTemplateName,
    TimeOnly? CheckInTime,
    TimeOnly? CheckOutTime,
    AttendanceStatus Status,
    string? Notes,
    string? OverrideReason,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record EmployeeAttendanceSummaryDto(
    int EmployeeInformationId,
    string EmployeeFullName,
    int PresentDays,
    int AbsentDays,
    int LateDays,
    int HalfDays,
    int OnLeaveDays,
    int EarlyLeaveDays,
    int TotalRecords);

public sealed record OvertimeRequestDto(
    int Id,
    int EmployeeInformationId,
    string EmployeeFullName,
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    decimal OtHours,
    OtRequestStatus Status,
    string? Reason,
    string? ReviewNote,
    int? ReviewedByUserId,
    DateTime? ReviewedAt,
    DateTime CreatedAt);

public sealed record LeaveTypeDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    bool IsPaid,
    decimal? PaidRate,
    decimal? MaxDaysPerRequest,
    bool RequireDocument,
    bool DeductsBalance,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record LeaveRequestDto(
    int Id,
    int EmployeeInformationId,
    string EmployeeFullName,
    int LeaveTypeId,
    string LeaveTypeCode,
    string LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalDays,
    bool IsPaid,
    LeaveRequestStatus Status,
    string? Reason,
    string? SupportingDocumentUrl,
    string? ReviewNote,
    int? ReviewedByUserId,
    DateTime? ReviewedAt,
    DateTime CreatedAt);

public sealed record LeaveBalanceDto(
    int Id,
    int EmployeeInformationId,
    int Year,
    int LeaveTypeId,
    string LeaveTypeCode,
    string LeaveTypeName,
    decimal BaseDays,
    decimal SeniorityBonusDays,
    decimal TotalDays,
    decimal UsedDays,
    decimal RemainingDays);

public sealed record AttendanceAdjustmentDto(
    int Id,
    int AttendanceRecordId,
    int EmployeeInformationId,
    string EmployeeFullName,
    DateOnly WorkDate,
    AttendanceStatus NewStatus,
    TimeOnly? NewCheckInTime,
    TimeOnly? NewCheckOutTime,
    AttendanceStatus? OriginalStatus,
    TimeOnly? OriginalCheckInTime,
    TimeOnly? OriginalCheckOutTime,
    string Reason,
    AdjustmentStatus Status,
    string? ReviewNote,
    int? ReviewedByUserId,
    DateTime? ReviewedAt,
    DateTime CreatedAt);
