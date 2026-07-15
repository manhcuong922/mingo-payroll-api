using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceRule.Queries;

public class GetAttendanceRuleVersionsQueryHandler
    : IRequestHandler<GetAttendanceRuleVersionsQuery, IReadOnlyList<AttendanceRuleVersionDto>?>
{
    private readonly IAttendanceRuleRepository _repository;
    private readonly IAttendanceRuleVersionRepository _versionRepository;

    public GetAttendanceRuleVersionsQueryHandler(
        IAttendanceRuleRepository repository,
        IAttendanceRuleVersionRepository versionRepository)
    {
        _repository = repository;
        _versionRepository = versionRepository;
    }

    public async Task<IReadOnlyList<AttendanceRuleVersionDto>?> Handle(
        GetAttendanceRuleVersionsQuery request, CancellationToken cancellationToken)
    {
        var rule = await _repository.GetByIdAsync(request.RuleId, cancellationToken);
        if (rule is null) return null;

        var map = await _versionRepository.GetByRuleIdsAsync(new[] { request.RuleId }, cancellationToken);
        if (!map.TryGetValue(request.RuleId, out var versions))
            return Array.Empty<AttendanceRuleVersionDto>();

        // Newest-first: open version trên đầu, closed version sắp theo EffectiveFrom desc.
        return versions
            .OrderByDescending(v => v.EffectiveTo == null)
            .ThenByDescending(v => v.EffectiveFrom)
            .Select(v => new AttendanceRuleVersionDto(
                v.Id,
                v.EffectiveFrom,
                v.EffectiveTo,
                v.WorkRateNormal,
                v.WorkRateWeeklyRest,
                v.WorkRateHoliday,
                v.CreatedAt,
                v.CreatedByUserId))
            .ToList();
    }
}
