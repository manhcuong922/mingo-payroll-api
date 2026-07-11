using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Commands;

public class CancelLateEarlyRequestCommandHandler : IRequestHandler<CancelLateEarlyRequestCommand, bool>
{
    private readonly ILateEarlyRequestRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CancelLateEarlyRequestCommandHandler(
        ILateEarlyRequestRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(CancelLateEarlyRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (existing.CreatedByUserId != userId)
            throw new ForbiddenAccessException("Chỉ người tạo đơn mới được hủy.");

        if (existing.Status != LateEarlyRequestStatus.Pending)
            throw new DomainValidationException(
                $"Chỉ được hủy khi đơn ở trạng thái Pending. Trạng thái hiện tại: {existing.Status}.");

        var now = _clock.UtcNow;
        existing.Status = LateEarlyRequestStatus.Cancelled;
        existing.CancelledAt = now;
        existing.CancelledByUserId = userId;
        existing.CancelReason = request.CancelReason?.Trim();
        existing.UpdatedAt = now;
        existing.UpdatedByUserId = userId;

        return await _repository.UpdateAsync(existing, cancellationToken);
    }
}
