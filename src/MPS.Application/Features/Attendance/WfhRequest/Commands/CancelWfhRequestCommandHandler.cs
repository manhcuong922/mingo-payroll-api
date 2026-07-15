using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.WfhRequest.Commands;

public class CancelWfhRequestCommandHandler : IRequestHandler<CancelWfhRequestCommand, bool>
{
    private readonly IWfhRequestRepository _repository;
    private readonly IAttendanceEditLockChecker _editLockChecker;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CancelWfhRequestCommandHandler(
        IWfhRequestRepository repository,
        IAttendanceEditLockChecker editLockChecker,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _editLockChecker = editLockChecker;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(CancelWfhRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (existing.CreatedByUserId != userId)
            throw new ForbiddenAccessException("Chỉ người tạo đơn mới được hủy.");

        var today = DateOnly.FromDateTime(_clock.UtcNow.AddHours(7));
        switch (existing.Status)
        {
            case WfhRequestStatus.Pending:
                break;

            case WfhRequestStatus.Approved:
                if (existing.StartDate <= today)
                    throw new DomainValidationException(
                        "Đơn WFH đã duyệt chỉ được hủy trước ngày bắt đầu.");
                await _editLockChecker.EnsureCanModifyAsync(existing.StartDate, overrideReason: null, cancellationToken);
                break;

            default:
                throw new DomainValidationException(
                    $"Không thể hủy đơn WFH ở trạng thái {existing.Status}.");
        }

        var now = _clock.UtcNow;
        existing.Status = WfhRequestStatus.Cancelled;
        existing.CancelledAt = now;
        existing.CancelledByUserId = userId;
        existing.CancelReason = request.CancelReason?.Trim();
        existing.UpdatedAt = now;
        existing.UpdatedByUserId = userId;

        return await _repository.UpdateAsync(existing, cancellationToken);
    }
}
