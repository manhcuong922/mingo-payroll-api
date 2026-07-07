using MediatR;

namespace MPS.Application.Features.Attendance.WeeklyDayOffRule.Commands;

public interface IWeeklyDayOffRuleWriteCommand
{
    IReadOnlyList<int> DaysOfWeek { get; }
    string ApplyScope { get; }
    string? TargetDepartmentIds { get; }
    string? TargetEmployeeIds { get; }
    DateOnly EffectiveFrom { get; }
    DateOnly? EffectiveTo { get; }
}

public sealed record CreateWeeklyDayOffRuleCommand(
    string Name,
    string? Description,
    IReadOnlyList<int> DaysOfWeek,
    string ApplyScope,
    string? TargetDepartmentIds,
    string? TargetEmployeeIds,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo) : IRequest<int>, IWeeklyDayOffRuleWriteCommand;
