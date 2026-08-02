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
    IReadOnlyList<int>? UsedByShiftTemplateIds = null,
    IReadOnlyList<AttendanceRuleVersionDto>? VersionHistory = null);

public sealed record AttendanceRuleVersionDto(
    int Id,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal WorkRateNormal,
    decimal WorkRateWeeklyRest,
    decimal WorkRateHoliday,
    DateTime CreatedAt,
    int CreatedByUserId);

public sealed record EmployeeShiftAssignmentDto(
    int Id,
    int EmployeeInformationId,
    string EmployeeFullName,
    int ShiftTemplateId,
    string ShiftTemplateName,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly WorkDate,
    DateTime CreatedAt,
    /// <summary>
    /// FK về ScheduleTemplate sinh ra assignment (null = manual/orphan).
    /// Cho phép FE nhận biết assignment thuộc template nào để filter view theo phòng ban.
    /// </summary>
    int? ScheduleTemplateId,
    /// <summary>
    /// applyTarget của template (null khi <see cref="ScheduleTemplateId"/> null).
    /// View "Tổng hợp theo phòng ban" chỉ include row có <c>Departments</c>.
    /// </summary>
    Domain.Enums.Attendance.ScheduleApplyTarget? ScheduleApplyTarget);

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
    DateTime? UpdatedAt,
    ScheduleApplyTarget? ApplyTarget,
    IReadOnlyList<int> TargetDepartmentIds,
    bool IncludeRestDaysAndHolidays);

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
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Kết quả parse file Excel import PublicHoliday (không xét DB).
/// </summary>
public sealed record PublicHolidayImportParseResult(
    IReadOnlyList<PublicHolidayImportParsedRow> Rows,
    IReadOnlyList<PublicHolidayImportRowError> GlobalErrors);

public sealed record PublicHolidayImportParsedRow(
    int RowIndex,
    string? Name,
    DateOnly? HolidayDate,
    DateOnly? HolidayEndDate,
    string? Description,
    IReadOnlyList<PublicHolidayImportRowError> RowErrors);

public sealed record PublicHolidayImportRowError(int? RowIndex, string Code, string Message);

/// <summary>
/// DTO trả về cho FE khi preview import: gồm các row hợp lệ + các row có lỗi (parse/validate/overlap).
/// </summary>
public sealed record PublicHolidayImportPreviewDto(
    IReadOnlyList<PublicHolidayImportPreviewRow> Rows,
    IReadOnlyList<PublicHolidayImportRowError> GlobalErrors,
    int ValidCount,
    int InvalidCount);

public sealed record PublicHolidayImportPreviewRow(
    int RowIndex,
    string? Name,
    DateOnly? HolidayDate,
    DateOnly? HolidayEndDate,
    string? Description,
    bool IsValid,
    IReadOnlyList<PublicHolidayImportRowError> Errors);

/// <summary>
/// DTO client gửi lên khi confirm — mỗi row là 1 holiday sẽ được insert. BE re-validate.
/// </summary>
public sealed record PublicHolidayImportConfirmRow(
    string Name,
    DateOnly HolidayDate,
    DateOnly? HolidayEndDate,
    string? Description);

public sealed record PublicHolidayImportConfirmResultDto(
    int Created,
    int Skipped,
    IReadOnlyList<PublicHolidayImportRowError> SkippedReasons);

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
    DateTime? UpdatedAt,
    // OT metadata — null cho record ca chính
    int? OvertimeRequestId = null,
    TimeOnly? OtStartTime = null,
    TimeOnly? OtEndTime = null,
    string? OvertimeProjectTitle = null,
    // BR-23: chỉ true khi Status=OnLeave và ngày đó thuộc đơn nghỉ Approved + IsPaid.
    // FE dùng để phân biệt "Nghỉ phép có lương" vs "Nghỉ phép không lương" trên bảng chấm công.
    bool IsPaidLeave = false);

/// <summary>
/// OT Approved chưa được chấm công cho 1 ngày — FE hiển thị row riêng trong daily
/// roster để HR click chấm. Sau khi chấm → AttendanceRecord tạo với OvertimeRequestId.
/// </summary>
public sealed record PendingOvertimeAttendanceDto(
    int OvertimeRequestId,
    int EmployeeInformationId,
    string EmployeeFullName,
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    decimal OtHours,
    int? ProjectId,
    string? ProjectTitle);

public sealed record EmployeeAttendanceSummaryDto(
    // Định danh
    int EmployeeInformationId,
    string EmployeeFullName,
    string? EmployeeCode,
    int? DepartmentId,
    string? DepartmentName,
    string? PositionName,

    // Status-based counts — reused bởi Báo cáo cũ (AttendanceReportPanel)
    int PresentDays,
    int AbsentDays,
    int LateDays,
    int HalfDays,
    int OnLeaveDays,
    int EarlyLeaveDays,
    int TotalRecords,

    // DayType-based aggregations — cho Bảng chấm công tháng.
    // Halfday cộng 0.5 vào DayType tương ứng của ngày đó.
    decimal NormalDays,
    decimal WeeklyRestDays,
    decimal HolidayDays,
    decimal PaidLeaveDays,
    // Số ngày status=OnLeave nhưng KHÔNG thuộc đơn nghỉ có lương (nghỉ không lương).
    // Tách riêng để bảng chấm công tháng phân biệt: paid = có trả công, unpaid = trọng số 0.
    decimal UnpaidLeaveDays,

    // Giờ công (từ AttendanceRecord.DayHours/NightHours).
    decimal DayHours,
    decimal NightHours,
    decimal TotalHours,

    // Quy đổi × hệ số (WorkRateNormal/WeeklyRest/Holiday × hours + NightHours × 1.3).
    // Trả decimal giờ; FE chia StandardHours nếu muốn hiển thị "công".
    decimal EquivalentHours,

    // Giờ OT — subset của TotalHours, filter records có OvertimeRequestId != null.
    // Dùng cho sort "OT nhiều nhất" trên bảng chấm công tháng.
    decimal OtHours);

public sealed record AttendanceSettingDto(
    int Id,
    bool IsEditLockEnabled,
    TimeOnly DailyEditCutoffTime,
    int LockAfterDays,
    bool AllowAdminOverride,
    bool RequireOverrideReason,
    int? LateEarlyMaxOccurrencesPerMonth,
    int? LateEarlyMaxMinutesPerOccurrence,
    int? LateEarlyBackdateDaysAllowed,
    decimal? WfhMaxDaysPerMonth,
    int? WfhMinNoticeDays,
    decimal? WfhMaxDaysPerRequest,
    int? WfhWorkPlanMinLength,
    int? LateGraceMinutes,
    int? EarlyLeaveGraceMinutes,
    int? HalfDayThresholdMinutes,
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
    // Snapshot version LaborLawConstants — composer dùng để compute multiplier từ công thức
    int? LaborLawConstantsVersionId,
    OtRequestStatus Status,
    string? Reason,
    string? ReviewNote,
    int? ReviewedByUserId,
    DateTime? ReviewedAt,
    DateTime CreatedAt,
    // v3 workflow — nullable cho legacy records
    int? ProjectId,
    string? ProjectTitle,
    OvertimeApproverRoute? ApproverRoute,
    Guid? BatchId,
    // v3 audit tầng 1 — PM (tổ trưởng đơn) review
    int? PmReviewedByUserId,
    DateTime? PmReviewedAt,
    string? PmReviewNote,
    // v3 audit tầng 2 — Approver (HR hoặc DM) decide
    int? ApproverReviewedByUserId,
    DateTime? ApproverReviewedAt,
    string? ApproverReviewNote);

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
    int? MinNoticeDaysForShort,
    int? MinNoticeDaysForMedium,
    int? MinNoticeDaysForLong,
    bool AutoFallbackToUnpaid,
    bool AllowRetroactive,
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
    OvertimeApproverRoute? ApproverRoute,
    LeaveRequestStatus Status,
    string? Reason,
    bool IsRetroactive,
    string? MedicalCertificateObjectKey,
    string? InsuranceCertNumber,
    string? ReviewNote,
    int? ReviewedByUserId,
    DateTime? ReviewedAt,
    DateTime? CancelledAt,
    int? CancelledByUserId,
    string? CancelReason,
    DateTime CreatedAt);

public sealed record WfhRequestDto(
    int Id,
    int EmployeeInformationId,
    string EmployeeFullName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalDays,
    string WorkPlan,
    OvertimeApproverRoute? ApproverRoute,
    WfhRequestStatus Status,
    string? Reason,
    string? ReviewNote,
    int? ReviewedByUserId,
    DateTime? ReviewedAt,
    DateTime? CancelledAt,
    int? CancelledByUserId,
    string? CancelReason,
    DateTime CreatedAt);

public sealed record LateEarlyRequestDto(
    int Id,
    int EmployeeInformationId,
    string EmployeeFullName,
    DateOnly WorkDate,
    LateEarlyKind Kind,
    short Minutes,
    bool IsUnplanned,
    bool DeductFromLeaveBalance,
    OvertimeApproverRoute? ApproverRoute,
    LateEarlyRequestStatus Status,
    string? Reason,
    string? ReviewNote,
    int? ReviewedByUserId,
    DateTime? ReviewedAt,
    DateTime? CancelledAt,
    int? CancelledByUserId,
    string? CancelReason,
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
