using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// PM tự huỷ đơn OT của mình khi còn Pending. Guards:
///  - Chỉ CreatedByUserId = current user mới huỷ được (không xuyên PM).
///  - Chỉ status = Pending. Approved/Rejected/Cancelled không huỷ lại được.
/// Set status = Cancelled thay vì hard delete để giữ audit trail.
/// </summary>
public class CancelOvertimeRequestCommandHandler : IRequestHandler<CancelOvertimeRequestCommand, bool>
{
    private readonly IOvertimeRequestRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CancelOvertimeRequestCommandHandler(
        IOvertimeRequestRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(CancelOvertimeRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (existing.CreatedByUserId != userId)
            throw new ForbiddenAccessException("Chỉ người tạo đơn mới được huỷ.");

        if (existing.Status != OtRequestStatus.Pending)
            throw new DomainValidationException(
                $"Chỉ huỷ được đơn ở trạng thái Chờ duyệt. Trạng thái hiện tại: {existing.Status}.");

        var now = _clock.UtcNow;
        existing.Status = OtRequestStatus.Cancelled;
        existing.UpdatedAt = now;
        existing.UpdatedByUserId = userId;

        await _repository.UpdateAsync(existing, cancellationToken);
        return true;
    }
}
