using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

public class CancelLeaveRequestCommandHandler : IRequestHandler<CancelLeaveRequestCommand, bool>
{
    private readonly ILeaveRequestRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CancelLeaveRequestCommandHandler(
        ILeaveRequestRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public Task<bool> Handle(CancelLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        return _repository.CancelAsync(
            request.Id, userId, request.CancelReason?.Trim(), _clock.UtcNow, cancellationToken);
    }
}
