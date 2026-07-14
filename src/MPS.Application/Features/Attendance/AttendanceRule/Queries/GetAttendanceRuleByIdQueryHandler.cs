using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceRule.Queries;

public class GetAttendanceRuleByIdQueryHandler : IRequestHandler<GetAttendanceRuleByIdQuery, AttendanceRuleDto?>
{
    private readonly IAttendanceRuleRepository _repository;
    private readonly IAttendanceRuleVersionRepository _versionRepository;

    public GetAttendanceRuleByIdQueryHandler(
        IAttendanceRuleRepository repository,
        IAttendanceRuleVersionRepository versionRepository)
    {
        _repository = repository;
        _versionRepository = versionRepository;
    }

    public async Task<AttendanceRuleDto?> Handle(GetAttendanceRuleByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity is null) return null;

        var versionsMap = await _versionRepository.GetByRuleIdsAsync(new[] { request.Id }, cancellationToken);
        var allVersions = versionsMap.TryGetValue(request.Id, out var list)
            ? list
            : Array.Empty<Domain.Entities.Attendance.AttendanceRuleVersion>();

        var openVersion = allVersions.FirstOrDefault(v => v.EffectiveTo == null);
        var usedBy = await _repository.GetShiftTemplateIdsUsingRuleAsync(request.Id, cancellationToken);

        // Newest-first cho FE render section "Lịch sử hệ số".
        var history = allVersions
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

        return new AttendanceRuleDto(
            entity.Id,
            entity.Name,
            entity.Description,
            openVersion?.WorkRateNormal ?? 0m,
            openVersion?.WorkRateWeeklyRest ?? 0m,
            openVersion?.WorkRateHoliday ?? 0m,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt,
            usedBy,
            history);
    }
}
