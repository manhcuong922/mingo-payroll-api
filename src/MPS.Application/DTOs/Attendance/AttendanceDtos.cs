using MPS.Domain.Enums.Attendance;

namespace MPS.Application.DTOs.Attendance;

/// <summary>
/// ShiftTemplate response — nest <see cref="AttendanceRuleSummaryDto"/> để FE
/// có sẵn rate mà không cần gọi 2 endpoint. Rate ở rule là single source of truth
/// sau §11 cleanup; ShiftTemplate KHÔNG còn lưu rate riêng.
/// </summary>
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
    int AppliedAttendanceRuleId,
    AttendanceRuleSummaryDto AppliedAttendanceRule,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AttendanceRuleSummaryDto(
    int Id,
    string Name,
    decimal WorkRateNormal,
    decimal WorkRateWeeklyRest,
    decimal WorkRateHoliday,
    bool IsActive);

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

public sealed record ScheduleTemplateShiftDto(
    int ShiftTemplateId,
    string ShiftTemplateCode,
    string ShiftTemplateName,
    TimeOnly StartTime,
    TimeOnly EndTime);

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
    IReadOnlyList<int> ShiftTemplateIds,
    IReadOnlyList<ScheduleTemplateShiftDto> Shifts,
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
    DateOnly HolidayEndDate,
    string? Description,
    string ApplyScope,
    string? TargetDepartmentIds,
    string? TargetEmployeeIds,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record WeeklyDayOffRuleDto(
    int Id,
    string Name,
    string? Description,
    IReadOnlyList<int> DaysOfWeek,
    string ApplyScope,
    string? TargetDepartmentIds,
    string? TargetEmployeeIds,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record WeeklyDayOffResolveDto(
    int EmployeeInformationId,
    DateOnly Date,
    IReadOnlyList<int> DaysOfWeek,
    bool IsWeeklyRestDay);

public sealed record AttendanceRecordDto(
    int Id,
    int EmployeeInformationId,
    string EmployeeFullName,
    DateOnly WorkDate,
    int? ShiftTemplateId,
    string? ShiftTemplateName,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    decimal DayHours,
    decimal NightHours,
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

public sealed record AttendanceSettingDto(
    int Id,
    bool IsEditLockEnabled,
    TimeOnly DailyEditCutoffTime,
    int LockAfterDays,
    bool AllowAdminOverride,
    bool RequireOverrideReason,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AttendanceLockStatusDto(
    DateOnly WorkDate,
    bool IsEditLockEnabled,
    bool IsLocked,
    DateTime? LockedAtUtc,
    DateTime? LockedAtLocal,
    bool CanOverride,
    bool RequiresOverrideReason,
    string? Message);

public sealed record OvertimeRequestDto(
    int Id,
    int EmployeeInformationId,
    string EmployeeFullName,
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    decimal OtHours,
    decimal OtDayHours,
    decimal OtNightHours,
    int? OvertimeSlotTemplateId,
    OvertimeSlotSummaryDto? OvertimeSlotTemplate,
    OtRequestStatus Status,
    string? Reason,
    string? ReviewNote,
    int? ReviewedByUserId,
    DateTime? ReviewedAt,
    DateTime CreatedAt);

public sealed record OvertimeSlotSummaryDto(
    int Id,
    string Code,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsActive);

public sealed record OvertimeSlotTemplateDto(
    int Id,
    string Code,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsNightSlot,
    // Override multiplier %; null = dùng sàn luật (composer Phase C resolve).
    decimal? MultiplierNormalDay,
    decimal? MultiplierWeeklyRestDay,
    decimal? MultiplierHolidayDay,
    decimal? MultiplierNormalNightNoPrevOt,
    decimal? MultiplierNormalNightWithPrevOt,
    decimal? MultiplierWeeklyRestNight,
    decimal? MultiplierHolidayNight,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record LeaveTypeDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    bool IsPaid,
    decimal? PaidRate,
    decimal? MaxDaysPerRequest,
    bool AllowEmployeeSelection,
    decimal? MaxDaysPerYear,
    decimal? MaxDaysPerMonth,
    decimal? MaxDaysPerWeek,
    string ApplyScope,
    string? TargetDepartmentIds,
    string? TargetEmployeeIds,
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

public sealed record EmployeeLeaveYearlySummaryDto(
    int EmployeeInformationId,
    string EmployeeFullName,
    string? EmployeeCode,
    int Year,
    IReadOnlyList<decimal> MonthlyApprovedDays,
    decimal TotalApprovedDays,
    int ApprovedRequestCount,
    decimal PendingDays,
    int PendingRequestCount);

public sealed record AttendanceAdjustmentDto(
    int Id,
    int AttendanceRecordId,
    int EmployeeInformationId,
    string EmployeeFullName,
    DateOnly WorkDate,
    AttendanceStatus NewStatus,
    DateTime? NewCheckInAt,
    DateTime? NewCheckOutAt,
    AttendanceStatus? OriginalStatus,
    DateTime? OriginalCheckInAt,
    DateTime? OriginalCheckOutAt,
    string Reason,
    AdjustmentStatus Status,
    string? ReviewNote,
    int? ReviewedByUserId,
    DateTime? ReviewedAt,
    DateTime CreatedAt);
