using MediatR;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Commands;

public sealed record RejectAttendanceAdjustmentCommand(int Id, string? ReviewNote) : IRequest<bool>;
