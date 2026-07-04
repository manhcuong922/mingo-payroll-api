using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Commands;

public sealed record UpdateAttendanceCommand(
    int EmployeeInformationId,
    DateOnly WorkDate,
    int ShiftTemplateId,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    AttendanceStatus Status,
    string? Notes,
    string? OverrideReason = null) : IRequest<bool>;
