using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using PublicHolidayEntity = MPS.Domain.Entities.Attendance.PublicHoliday;

namespace MPS.Application.Features.Attendance.PublicHoliday.Commands;

public class CreatePublicHolidayCommandHandler : IRequestHandler<CreatePublicHolidayCommand, int>
{
    private const int MaxRangeDays = 366;

    private readonly IPublicHolidayRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CreatePublicHolidayCommandHandler(
        IPublicHolidayRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(CreatePublicHolidayCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        EnsureValidRange(request.HolidayDate, request.HolidayEndDate);

        if (await _repository.ExistsOverlapAsync(request.HolidayDate, request.HolidayEndDate, excludeId: null, cancellationToken))
            throw new ConflictException($"A public holiday already overlaps {request.HolidayDate:yyyy-MM-dd} - {request.HolidayEndDate:yyyy-MM-dd}.");

        var entity = new PublicHolidayEntity
        {
            Name = request.Name.Trim(),
            HolidayDate = request.HolidayDate,
            HolidayEndDate = request.HolidayEndDate,
            Description = request.Description,
            ApplyScope = NormalizeScope(request.ApplyScope),
            TargetDepartmentIds = NormalizeTargets(request.TargetDepartmentIds, request.ApplyScope, "SpecificDepartments"),
            TargetEmployeeIds = NormalizeTargets(request.TargetEmployeeIds, request.ApplyScope, "SpecificEmployees"),
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }

    private static void EnsureValidRange(DateOnly start, DateOnly end)
    {
        if (end < start)
            throw new DomainValidationException("HolidayEndDate must be on/after HolidayDate.");

        var count = end.DayNumber - start.DayNumber + 1;
        if (count > MaxRangeDays)
            throw new DomainValidationException($"Holiday range cannot exceed {MaxRangeDays} days.");
    }

    private static string NormalizeScope(string? scope) =>
        string.IsNullOrWhiteSpace(scope) ? "AllEmployees" : scope.Trim();

    private static string? NormalizeTargets(string? ids, string? scope, string specificScope) =>
        NormalizeScope(scope) == specificScope && !string.IsNullOrWhiteSpace(ids)
            ? ids.Trim()
            : null;
}
