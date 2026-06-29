using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceRule.Queries;

public class GetAttendanceRulesQueryHandler : IRequestHandler<GetAttendanceRulesQuery, PagedResult<AttendanceRuleDto>>
{
    private readonly IAttendanceRuleRepository _repository;

    public GetAttendanceRulesQueryHandler(IAttendanceRuleRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<AttendanceRuleDto>> Handle(GetAttendanceRulesQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);

        var (items, total) = await _repository.GetPagedAsync(request.IsActive, page, size, cancellationToken);

        var dtos = items.Select(r => new AttendanceRuleDto(
            r.Id,
            r.Name,
            r.Description,
            r.WorkRateNormal,
            r.WorkRateWeeklyRest,
            r.WorkRateHoliday,
            r.WorkRateNight,
            r.IsActive,
            r.CreatedAt,
            r.UpdatedAt,
            null)).ToList();

        return new PagedResult<AttendanceRuleDto>(dtos, total, page, size);
    }
}
