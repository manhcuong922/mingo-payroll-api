using MediatR;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Commands;

public sealed record ApproveAttendanceAdjustmentCommand(int Id, string? ReviewNote) : IRequest<bool>;