using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Queries;

public class GetShiftTemplateByIdQueryHandler : IRequestHandler<GetShiftTemplateByIdQuery, ShiftTemplateDto?>
{
    private readonly IShiftTemplateRepository _repository;

    public GetShiftTemplateByIdQueryHandler(IShiftTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<ShiftTemplateDto?> Handle(GetShiftTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity is null) return null;

        return new ShiftTemplateDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.StartTime,
            entity.EndTime,
            entity.BreakStartTime,
            entity.BreakEndTime,
            entity.StandardHours,
            entity.StandardDays,
            entity.WorkRateNormal,
            entity.WorkRateWeeklyRest,
            entity.WorkRateHoliday,
            entity.IsNightShift,
            entity.AppliedAttendanceRuleId,
            entity.IsActive,
            entity.Notes,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
