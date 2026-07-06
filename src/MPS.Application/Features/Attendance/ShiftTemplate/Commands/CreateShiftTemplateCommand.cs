using MediatR;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Commands;

public sealed record CreateShiftTemplateCommand(
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeOnly? BreakStartTime,
    TimeOnly? BreakEndTime,
    decimal StandardHours,
    decimal StandardDays,
    int AppliedAttendanceRuleId,
    string? Notes) : IRequest<int>, IShiftTemplateCommand;
