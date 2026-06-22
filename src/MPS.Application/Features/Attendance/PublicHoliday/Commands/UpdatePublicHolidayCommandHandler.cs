using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using PublicHolidayEntity = MPS.Domain.Entities.Attendance.PublicHoliday;

namespace MPS.Application.Features.Attendance.PublicHoliday.Commands;

public class UpdatePublicHolidayCommandHandler : IRequestHandler<UpdatePublicHolidayCommand, bool>
{
    private readonly IPublicHolidayRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public UpdatePublicHolidayCommandHandler(
        IPublicHolidayRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(UpdatePublicHolidayCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (await _repository.ExistsByDateAsync(request.HolidayDate, excludeId: request.Id, cancellationToken))
            throw new ConflictException($"A public holiday already exists on {request.HolidayDate:yyyy-MM-dd}.");

        if (request.PayRateOverride is { } rate && rate < 1.0m)
            throw new DomainValidationException("PayRateOverride phải ≥ 1.0 nếu được set.");

        var entity = new PublicHolidayEntity
        {
            Id = request.Id,
            Name = request.Name.Trim(),
            HolidayDate = request.HolidayDate,
            Category = request.Category,
            PayRateOverride = request.PayRateOverride,
            Description = request.Description,
            IsActive = existing.IsActive,
            UpdatedAt = _clock.UtcNow,
            UpdatedByUserId = userId
        };

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
