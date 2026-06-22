using MediatR;

namespace MPS.Application.Features.Attendance.AttendanceRule.Commands;

public sealed record DeleteAttendanceRuleCommand(int Id) : IRequest<bool>;
