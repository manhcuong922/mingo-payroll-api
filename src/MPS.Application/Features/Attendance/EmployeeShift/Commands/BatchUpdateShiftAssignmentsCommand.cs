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
    OverlapsExistingShift = 9
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
/// Batch phân ca với cap §9 Điều 105 BLLĐ — warn-then-confirm khi vượt 48h/tuần regular.
/// Khi <see cref="AcknowledgeWeeklyOvertimeWarnings"/> = false và cap bị vượt, handler throw
/// <c>WeeklyRegularHoursCapWarningException</c> → middleware trả 409 + warning list để FE
/// hiển thị popup xác nhận. FE retry với flag = true để apply.
/// </summary>
public sealed record BatchUpdateShiftAssignmentsCommand(
    IReadOnlyList<BatchShiftOperation> Operations,
    BatchShiftConflictPolicy ConflictPolicy,
    int? SourceScheduleTemplateId = null,
    bool AcknowledgeWeeklyOvertimeWarnings = false) : IRequest<BatchUpdateShiftAssignmentsResult>;
