using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LeaveType.Commands;

public class DeleteLeaveTypeCommandHandler : IRequestHandler<DeleteLeaveTypeCommand, bool>
{
    private readonly ILeaveTypeRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public DeleteLeaveTypeCommandHandler(
        ILeaveTypeRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(DeleteLeaveTypeCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        return await _repository.DeactivateAsync(request.Id, userId, _clock.UtcNow, cancellationToken);
    }
}
