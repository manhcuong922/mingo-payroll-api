using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceRule.Queries;

public class GetAttendanceRuleByIdQueryHandler : IRequestHandler<GetAttendanceRuleByIdQuery, AttendanceRuleDto?>
{
    private readonly IAttendanceRuleRepository _repository;

    public GetAttendanceRuleByIdQueryHandler(IAttendanceRuleRepository repository)
    {
        _repository = repository;
    }

    public async Task<AttendanceRuleDto?> Handle(GetAttendanceRuleByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity is null) return null;

        var usedBy = await _repository.GetShiftTemplateIdsUsingRuleAsync(request.Id, cancellationToken);

        return new AttendanceRuleDto(
            entity.Id, entity.Name, entity.Description,
            entity.WorkRateNormal, entity.WorkRateWeeklyRest, entity.WorkRateHoliday,
            entity.IsActive, entity.CreatedAt, entity.UpdatedAt, usedBy);
    }
}
