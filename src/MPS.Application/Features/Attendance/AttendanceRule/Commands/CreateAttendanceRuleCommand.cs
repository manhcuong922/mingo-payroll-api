using MediatR;

namespace MPS.Application.Features.Attendance.AttendanceRule.Commands;

public sealed record CreateAttendanceRuleCommand(
    string Name,
    string? Description,
    decimal WorkRateNormal,
    decimal WorkRateWeeklyRest,
    decimal WorkRateHoliday) : IRequest<int>;
