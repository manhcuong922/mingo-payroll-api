using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using AttendanceRuleEntity = MPS.Domain.Entities.Attendance.AttendanceRule;

namespace MPS.Application.Features.Attendance.AttendanceRule.Commands;

public class UpdateAttendanceRuleCommandHandler : IRequestHandler<UpdateAttendanceRuleCommand, bool>
{
    private readonly IAttendanceRuleRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IComplianceLookupService _complianceLookup;

    public UpdateAttendanceRuleCommandHandler(
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

    public async Task<bool> Handle(UpdateAttendanceRuleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        var name = request.Name.Trim();
        if (await _repository.ExistsByNameAsync(name, excludeId: request.Id, cancellationToken))
            throw new ConflictException($"Attendance rule with name '{name}' already exists.");

        var laborLaw = await _complianceLookup.GetActiveLaborLawConstantsAsync(cancellationToken);

        WorkRateFloorValidator.EnsureValid(
            request.WorkRateNormal,
            request.WorkRateWeeklyRest,
            request.WorkRateHoliday,
            laborLaw.WorkRateNormalFloor,
            laborLaw.WorkRateWeeklyRestFloor,
            laborLaw.WorkRateHolidayFloor);

        var entity = new AttendanceRuleEntity
        {
            Id = request.Id,
            Name = name,
            Description = request.Description,
            WorkRateNormal = request.WorkRateNormal,
            WorkRateWeeklyRest = request.WorkRateWeeklyRest,
            WorkRateHoliday = request.WorkRateHoliday,
            IsActive = request.IsActive,
            UpdatedAt = _clock.UtcNow,
            UpdatedByUserId = userId
        };

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
