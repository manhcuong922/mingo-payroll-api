using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.WeeklyDayOffRule.Common;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.WeeklyDayOffRule.Queries;

public class GetWeeklyDayOffRuleByIdQueryHandler : IRequestHandler<GetWeeklyDayOffRuleByIdQuery, WeeklyDayOffRuleDto?>
{
    private readonly IWeeklyDayOffRuleRepository _repository;

    public GetWeeklyDayOffRuleByIdQueryHandler(IWeeklyDayOffRuleRepository repository)
    {
        _repository = repository;
    }

    public async Task<WeeklyDayOffRuleDto?> Handle(GetWeeklyDayOffRuleByIdQuery request, CancellationToken cancellationToken)
    {
        var r = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (r is null) return null;

        return new WeeklyDayOffRuleDto(
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
            r.UpdatedAt);
    }
}
