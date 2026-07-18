using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.PublicHoliday.Commands;

public class DeletePublicHolidayCommandHandler : IRequestHandler<DeletePublicHolidayCommand, bool>
{
    private readonly IPublicHolidayRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public DeletePublicHolidayCommandHandler(
        IPublicHolidayRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(DeletePublicHolidayCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        return await _repository.DeactivateAsync(request.Id, userId, _clock.UtcNow, cancellationToken);
    }
}
