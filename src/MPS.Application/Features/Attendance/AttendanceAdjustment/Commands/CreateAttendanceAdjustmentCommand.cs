using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Commands;

public sealed record CreateAttendanceAdjustmentCommand(
    int AttendanceRecordId,
    AttendanceStatus NewStatus,
    DateTime? NewCheckInAt,
    DateTime? NewCheckOutAt,
    string Reason) : IRequest<int>;
