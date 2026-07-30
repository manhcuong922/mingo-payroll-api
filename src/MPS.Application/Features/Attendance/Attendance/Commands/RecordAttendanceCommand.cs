using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Commands;

/// <summary>
/// Chấm công cho 1 record — 2 mode dựa theo nguồn:
///  • Ca chính: ShiftTemplateId set, OvertimeRequestId null
///  • Chấm OT:  OvertimeRequestId set, ShiftTemplateId null (Approved OT do PM giao)
/// Handler validate exactly-one-of.
/// </summary>
public sealed record RecordAttendanceCommand(
    int EmployeeInformationId,
    DateOnly WorkDate,
    int? ShiftTemplateId,
    int? OvertimeRequestId,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    AttendanceStatus? Status,
    string? Notes,
    string? OverrideReason = null) : IRequest<int>;
