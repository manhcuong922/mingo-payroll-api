using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Commands;

public sealed record UpdateAttendanceCommand(
    int EmployeeInformationId,
    DateOnly WorkDate,
    int ShiftTemplateId,
    TimeOnly? CheckInTime,
    TimeOnly? CheckOutTime,
    AttendanceStatus Status,
    string? Notes,
    string? OverrideReason) : IRequest<bool>;
