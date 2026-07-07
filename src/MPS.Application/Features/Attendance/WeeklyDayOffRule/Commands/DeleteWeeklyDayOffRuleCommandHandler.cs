using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.WeeklyDayOffRule.Commands;

public class DeleteWeeklyDayOffRuleCommandHandler : IRequestHandler<DeleteWeeklyDayOffRuleCommand, bool>
{
    private readonly IWeeklyDayOffRuleRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public DeleteWeeklyDayOffRuleCommandHandler(
        IWeeklyDayOffRuleRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(DeleteWeeklyDayOffRuleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        return await _repository.DeactivateAsync(request.Id, userId, _clock.UtcNow, cancellationToken);
    }
}
