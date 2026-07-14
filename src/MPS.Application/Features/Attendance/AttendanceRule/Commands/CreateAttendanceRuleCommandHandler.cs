using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using AttendanceRuleEntity = MPS.Domain.Entities.Attendance.AttendanceRule;
using AttendanceRuleVersionEntity = MPS.Domain.Entities.Attendance.AttendanceRuleVersion;

namespace MPS.Application.Features.Attendance.AttendanceRule.Commands;

public class CreateAttendanceRuleCommandHandler : IRequestHandler<CreateAttendanceRuleCommand, int>
{
    private readonly IAttendanceRuleRepository _repository;
    private readonly IAttendanceRuleVersionRepository _versionRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IComplianceLookupService _complianceLookup;

    public CreateAttendanceRuleCommandHandler(
        IAttendanceRuleRepository repository,
        IAttendanceRuleVersionRepository versionRepository,
        ICurrentUserService currentUser,
        IClock clock,
        IComplianceLookupService complianceLookup)
    {
        _repository = repository;
        _versionRepository = versionRepository;
        _currentUser = currentUser;
        _clock = clock;
        _complianceLookup = complianceLookup;
    }

    public async Task<int> Handle(CreateAttendanceRuleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var name = request.Name.Trim();
        if (await _repository.ExistsByNameAsync(name, excludeId: null, cancellationToken))
            throw new ConflictException($"Attendance rule with name '{name}' already exists.");

        var laborLaw = await _complianceLookup.GetActiveLaborLawConstantsAsync(cancellationToken);

        WorkRateFloorValidator.EnsureValid(
            request.WorkRateNormal,
            request.WorkRateWeeklyRest,
            request.WorkRateHoliday,
            laborLaw.WorkRateNormalFloor,
            laborLaw.WorkRateWeeklyRestFloor,
            laborLaw.WorkRateHolidayFloor);

        var now = _clock.UtcNow;
        var today = DateOnly.FromDateTime(now);

        var rule = new AttendanceRuleEntity
        {
            Name = name,
            Description = request.Description,
            IsActive = true,
            CreatedAt = now,
            CreatedByUserId = userId
        };

        var ruleId = await _repository.AddAsync(rule, cancellationToken);

        var version = new AttendanceRuleVersionEntity
        {
            AttendanceRuleId = ruleId,
            EffectiveFrom = today,
            EffectiveTo = null,
            WorkRateNormal = request.WorkRateNormal,
            WorkRateWeeklyRest = request.WorkRateWeeklyRest,
            WorkRateHoliday = request.WorkRateHoliday,
            CreatedAt = now,
            CreatedByUserId = userId,
        };
        await _versionRepository.AddAsync(version, cancellationToken);

        return ruleId;
    }
}
