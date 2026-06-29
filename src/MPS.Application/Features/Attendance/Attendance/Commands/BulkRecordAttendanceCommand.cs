using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Commands;

public sealed record BulkRecordAttendanceItem(
    int EmployeeInformationId,
    int ShiftTemplateId,
    TimeOnly? CheckInTime,
    TimeOnly? CheckOutTime,
    AttendanceStatus Status,
    string? Notes,
    string? OverrideReason);

public sealed record BulkRecordAttendanceCommand(
    DateOnly WorkDate,
    IReadOnlyList<BulkRecordAttendanceItem> Records,
    bool AcknowledgeWeeklyOvertimeWarnings = false) : IRequest<BulkRecordAttendanceResult>;

public sealed record BulkRecordAttendanceItemResult(
    int EmployeeInformationId,
    int ShiftTemplateId,
    bool Success,
    string Action,
    string? Error);

public sealed record BulkRecordAttendanceResult(
    int Created,
    int Updated,
    int Failed,
    IReadOnlyList<BulkRecordAttendanceItemResult> Items);
