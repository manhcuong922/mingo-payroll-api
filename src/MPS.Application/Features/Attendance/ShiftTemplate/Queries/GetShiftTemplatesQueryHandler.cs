using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Queries;

public class GetShiftTemplatesQueryHandler : IRequestHandler<GetShiftTemplatesQuery, PagedResult<ShiftTemplateDto>>
{
    private readonly IShiftTemplateRepository _repository;
    private readonly IAttendanceRuleVersionRepository _versionRepository;

    public GetShiftTemplatesQueryHandler(
        IShiftTemplateRepository repository,
        IAttendanceRuleVersionRepository versionRepository)
    {
        _repository = repository;
        _versionRepository = versionRepository;
    }

    public async Task<PagedResult<ShiftTemplateDto>> Handle(GetShiftTemplatesQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);

        var (items, total) = await _repository.GetPagedAsync(request.IsActive, page, size, cancellationToken);

        var ruleIds = items.Select(i => i.AppliedAttendanceRuleId).Distinct().ToList();
        var openVersions = await _versionRepository.GetOpenByRuleIdsAsync(ruleIds, cancellationToken);

        var dtos = items.Select(i =>
        {
            openVersions.TryGetValue(i.AppliedAttendanceRuleId, out var v);
            return ShiftTemplateMapper.ToDto(i, v);
        }).ToList();

        return new PagedResult<ShiftTemplateDto>(dtos, total, page, size);
    }
}
