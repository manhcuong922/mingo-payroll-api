using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Commands;

public class DeleteShiftTemplateCommandHandler : IRequestHandler<DeleteShiftTemplateCommand, bool>
{
    private readonly IShiftTemplateRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public DeleteShiftTemplateCommandHandler(
        IShiftTemplateRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(DeleteShiftTemplateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        // Soft-delete (deactivate) if already in use; hard-delete otherwise
        if (await _repository.IsInUseAsync(request.Id, cancellationToken))
            return await _repository.DeactivateAsync(request.Id, userId, _clock.UtcNow, cancellationToken);

        return await _repository.HardDeleteAsync(request.Id, cancellationToken);
    }
}
