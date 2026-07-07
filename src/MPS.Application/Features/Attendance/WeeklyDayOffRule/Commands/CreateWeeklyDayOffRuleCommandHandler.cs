using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.WeeklyDayOffRule.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using WeeklyDayOffRuleEntity = MPS.Domain.Entities.Attendance.WeeklyDayOffRule;

namespace MPS.Application.Features.Attendance.WeeklyDayOffRule.Commands;

public class CreateWeeklyDayOffRuleCommandHandler : IRequestHandler<CreateWeeklyDayOffRuleCommand, int>
{
    private readonly IWeeklyDayOffRuleRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CreateWeeklyDayOffRuleCommandHandler(
        IWeeklyDayOffRuleRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(CreateWeeklyDayOffRuleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        EnsureValidRange(request.EffectiveFrom, request.EffectiveTo);
        EnsureValidDays(request.DaysOfWeek);

        var name = request.Name.Trim();
        if (await _repository.ExistsByNameAsync(name, excludeId: null, cancellationToken))
            throw new ConflictException($"A weekly day-off rule named '{name}' already exists.");

        var entity = new WeeklyDayOffRuleEntity
        {
            Name = name,
            Description = request.Description,
            DaysOfWeekMask = DaysOfWeekMask.Encode(request.DaysOfWeek),
            ApplyScope = NormalizeScope(request.ApplyScope),
            TargetDepartmentIds = NormalizeTargets(request.TargetDepartmentIds, request.ApplyScope, "SpecificDepartments"),
            TargetEmployeeIds = NormalizeTargets(request.TargetEmployeeIds, request.ApplyScope, "SpecificEmployees"),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }

    private static void EnsureValidRange(DateOnly from, DateOnly? to)
    {
        if (to.HasValue && to.Value < from)
            throw new DomainValidationException("EffectiveTo must be on/after EffectiveFrom.");
    }

    private static void EnsureValidDays(IReadOnlyList<int> daysOfWeek)
    {
        if (daysOfWeek is null || daysOfWeek.Count == 0)
            throw new DomainValidationException("DaysOfWeek must contain at least one day.");
        if (daysOfWeek.Any(d => d is < 0 or > 6))
            throw new DomainValidationException("DaysOfWeek must be integers between 0 (Sunday) and 6 (Saturday).");
    }

    private static string NormalizeScope(string? scope) =>
        string.IsNullOrWhiteSpace(scope) ? "AllEmployees" : scope.Trim();

    private static string? NormalizeTargets(string? ids, string? scope, string specificScope) =>
        NormalizeScope(scope) == specificScope && !string.IsNullOrWhiteSpace(ids)
            ? ids.Trim()
            : null;
}
