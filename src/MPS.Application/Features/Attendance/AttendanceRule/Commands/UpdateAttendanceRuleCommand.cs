using MediatR;

namespace MPS.Application.Features.Attendance.AttendanceRule.Commands;

public sealed record UpdateAttendanceRuleCommand(
    int Id,
    string Name,
    string? Description,
    decimal WorkRateNormal,
    decimal WorkRateWeeklyRest,
    decimal WorkRateHoliday,
    decimal WorkRateNight,
    bool IsActive) : IRequest<bool>;
