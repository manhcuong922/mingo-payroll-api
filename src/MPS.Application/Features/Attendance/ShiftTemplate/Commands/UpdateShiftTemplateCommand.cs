using MediatR;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Commands;

public sealed record UpdateShiftTemplateCommand(
    int Id,
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeOnly? BreakStartTime,
    TimeOnly? BreakEndTime,
    decimal StandardHours,
    decimal StandardDays,
    bool IsNightShift,
    int? AppliedAttendanceRuleId,
    bool IsActive,
    string? Notes) : IRequest<bool>, IShiftTemplateCommand;
