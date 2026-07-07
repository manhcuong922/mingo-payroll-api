using MediatR;

namespace MPS.Application.Features.Attendance.WeeklyDayOffRule.Commands;

public sealed record UpdateWeeklyDayOffRuleCommand(
    int Id,
    string Name,
    string? Description,
    IReadOnlyList<int> DaysOfWeek,
    string ApplyScope,
    string? TargetDepartmentIds,
    string? TargetEmployeeIds,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo) : IRequest<bool>, IWeeklyDayOffRuleWriteCommand;
