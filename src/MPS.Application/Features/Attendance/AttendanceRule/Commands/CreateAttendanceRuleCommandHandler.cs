using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using AttendanceRuleEntity = MPS.Domain.Entities.Attendance.AttendanceRule;

namespace MPS.Application.Features.Attendance.AttendanceRule.Commands;

public class CreateAttendanceRuleCommandHandler : IRequestHandler<CreateAttendanceRuleCommand, int>
{
    private readonly IAttendanceRuleRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IComplianceLookupService _complianceLookup;

    public CreateAttendanceRuleCommandHandler(
        IAttendanceRuleRepository repository,
        ICurrentUserService currentUser,
        IClock clock,
        IComplianceLookupService complianceLookup)
    {
        _repository = repository;
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
        var entity = new AttendanceRuleEntity
        {
            Name = name,
            Description = request.Description,
            WorkRateNormal = request.WorkRateNormal,
            WorkRateWeeklyRest = request.WorkRateWeeklyRest,
            WorkRateHoliday = request.WorkRateHoliday,
            IsActive = true,
            CreatedAt = now,
            CreatedByUserId = userId
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
