using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceRule.Queries;

public class GetAttendanceRulesQueryHandler : IRequestHandler<GetAttendanceRulesQuery, PagedResult<AttendanceRuleDto>>
{
    private readonly IAttendanceRuleRepository _repository;
    private readonly IAttendanceRuleVersionRepository _versionRepository;

    public GetAttendanceRulesQueryHandler(
        IAttendanceRuleRepository repository,
        IAttendanceRuleVersionRepository versionRepository)
    {
        _repository = repository;
        _versionRepository = versionRepository;
    }

    public async Task<PagedResult<AttendanceRuleDto>> Handle(GetAttendanceRulesQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);

        var (items, total) = await _repository.GetPagedAsync(request.IsActive, page, size, cancellationToken);

        var ruleIds = items.Select(r => r.Id).ToList();
        var openVersions = await _versionRepository.GetOpenByRuleIdsAsync(ruleIds, cancellationToken);

        var dtos = items.Select(r =>
        {
            openVersions.TryGetValue(r.Id, out var v);
            return new AttendanceRuleDto(
                r.Id,
                r.Name,
                r.Description,
                v?.WorkRateNormal ?? 0m,
                v?.WorkRateWeeklyRest ?? 0m,
                v?.WorkRateHoliday ?? 0m,
                r.IsActive,
                r.CreatedAt,
                r.UpdatedAt,
                null);
        }).ToList();

        return new PagedResult<AttendanceRuleDto>(dtos, total, page, size);
    }
}
