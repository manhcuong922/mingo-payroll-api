using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Commands;

public sealed record CreateAttendanceAdjustmentCommand(
    int AttendanceRecordId,
    AttendanceStatus NewStatus,
    TimeOnly? NewCheckInTime,
    TimeOnly? NewCheckOutTime,
    string Reason) : IRequest<int>;
