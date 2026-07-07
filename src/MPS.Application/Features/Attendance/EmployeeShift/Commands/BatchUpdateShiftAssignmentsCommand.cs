using MediatR;

namespace MPS.Application.Features.Attendance.EmployeeShift.Commands;

public enum BatchShiftOperationKind
{
    Add = 1,
    Change = 2,
    Remove = 3
}

public enum BatchShiftConflictPolicy
{
    Skip = 1,
    Fail = 2,
    ReplaceIfNoAttendance = 3
}

public enum BatchShiftItemStatus
{
    Applied = 1,
    Replaced = 2,
    SkippedSameShift = 3,
    ConflictDifferentShift = 4,
    AttendanceLocked = 5,
    NotFound = 6,
    InvalidShiftTemplate = 7,
    InvalidEmployee = 8,
    /// <summary>Multi-shift: ca mới giao thoa thời gian với ca đã có trên cùng ngày.</summary>
    OverlapsExistingShift = 9,
    /// <summary>Ngày rơi vào WeeklyRestDay của NV (theo WeeklyDayOffRule) — bỏ qua khi chưa acknowledge.</summary>
    SkippedWeeklyRestDay = 10,
    /// <summary>Ngày rơi vào PublicHoliday áp dụng cho NV — bỏ qua khi chưa acknowledge.</summary>
    SkippedPublicHoliday = 11
}

public sealed record BatchShiftOperation(
    BatchShiftOperationKind Kind,
    int EmployeeInformationId,
    DateOnly WorkDate,
    int? ShiftTemplateId);

public sealed record BatchShiftItemResult(
    BatchShiftOperationKind Kind,
    int EmployeeInformationId,
    DateOnly WorkDate,
    int? ShiftTemplateId,
    BatchShiftItemStatus Status,
    int? ExistingShiftTemplateId,
    string? Reason);

public sealed record BatchUpdateShiftAssignmentsResult(
    int Applied,
    int Skipped,
    int Conflicts,
    IReadOnlyList<BatchShiftItemResult> Items);

/// <summary>
/// Batch phân ca với cap §9 Điều 105 BLLĐ.
/// Vượt 8h/ngày hoặc 48h/tuần thì handler chặn cứng; flag acknowledge chỉ giữ
/// lại để tương thích request cũ và không còn bypass cap phân ca.
///
/// <see cref="IncludeRestDaysAndHolidays"/> — mặc định false: auto-skip các op Add rơi vào
/// WeeklyRestDay của NV hoặc PublicHoliday. Trả về status
/// <see cref="BatchShiftItemStatus.SkippedWeeklyRestDay"/> / <see cref="BatchShiftItemStatus.SkippedPublicHoliday"/>
/// để FE hỏi xác nhận. Set true để bỏ qua guard này và tạo assignment bình thường.
/// </summary>
public sealed record BatchUpdateShiftAssignmentsCommand(
    IReadOnlyList<BatchShiftOperation> Operations,
    BatchShiftConflictPolicy ConflictPolicy,
    int? SourceScheduleTemplateId = null,
    bool AcknowledgeWeeklyOvertimeWarnings = false,
    bool IncludeRestDaysAndHolidays = false) : IRequest<BatchUpdateShiftAssignmentsResult>;
