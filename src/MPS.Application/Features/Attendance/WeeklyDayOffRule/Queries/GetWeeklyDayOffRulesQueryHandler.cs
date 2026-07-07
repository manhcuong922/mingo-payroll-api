using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.WeeklyDayOffRule.Common;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.WeeklyDayOffRule.Queries;

public class GetWeeklyDayOffRulesQueryHandler : IRequestHandler<GetWeeklyDayOffRulesQuery, IReadOnlyList<WeeklyDayOffRuleDto>>
{
    private readonly IWeeklyDayOffRuleRepository _repository;

    public GetWeeklyDayOffRulesQueryHandler(IWeeklyDayOffRuleRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<WeeklyDayOffRuleDto>> Handle(GetWeeklyDayOffRulesQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.ListAsync(
            request.IncludeInactive,
            request.ApplyScope,
            request.EffectiveOn,
            cancellationToken);

        return items.Select(r => new WeeklyDayOffRuleDto(
            r.Id,
            r.Name,
            r.Description,
            DaysOfWeekMask.Decode(r.DaysOfWeekMask),
            r.ApplyScope,
            r.TargetDepartmentIds,
            r.TargetEmployeeIds,
            r.EffectiveFrom,
            r.EffectiveTo,
            r.IsActive,
            r.CreatedAt,
            r.UpdatedAt)).ToList();
    }
}
