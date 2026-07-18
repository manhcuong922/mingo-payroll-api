using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using PublicHolidayEntity = MPS.Domain.Entities.Attendance.PublicHoliday;

namespace MPS.Application.Features.Attendance.PublicHoliday.Commands;

public class UpdatePublicHolidayCommandHandler : IRequestHandler<UpdatePublicHolidayCommand, bool>
{
    private const int MaxRangeDays = 366;

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

        EnsureValidRange(request.HolidayDate, request.HolidayEndDate);

        // Chỉ check overlap khi bật hoạt động (row inactive không tham gia tính toán, tự do trùng).
        if (request.IsActive
            && await _repository.ExistsOverlapAsync(request.HolidayDate, request.HolidayEndDate, excludeId: request.Id, cancellationToken))
            throw new ConflictException($"A public holiday already overlaps {request.HolidayDate:yyyy-MM-dd} - {request.HolidayEndDate:yyyy-MM-dd}.");

        var entity = new PublicHolidayEntity
        {
            Id = request.Id,
            Name = request.Name.Trim(),
            HolidayDate = request.HolidayDate,
            HolidayEndDate = request.HolidayEndDate,
            Description = request.Description,
            IsActive = request.IsActive,
            UpdatedAt = _clock.UtcNow,
            UpdatedByUserId = userId
        };

        return await _repository.UpdateAsync(entity, cancellationToken);
    }

    private static void EnsureValidRange(DateOnly start, DateOnly end)
    {
        if (end < start)
            throw new DomainValidationException("HolidayEndDate must be on/after HolidayDate.");

        var count = end.DayNumber - start.DayNumber + 1;
        if (count > MaxRangeDays)
            throw new DomainValidationException($"Holiday range cannot exceed {MaxRangeDays} days.");
    }
}
