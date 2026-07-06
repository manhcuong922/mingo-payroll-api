using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Commands;

public sealed record OverrideAttendanceCommand(
    int EmployeeInformationId,
    DateOnly WorkDate,
    int ShiftTemplateId,
    AttendanceStatus NewStatus,
    DateTime? NewCheckInAt,
    DateTime? NewCheckOutAt,
    string OverrideReason) : IRequest<int>;
