using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Notifications;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Enums.Notifications;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Commands;

public class RejectAttendanceAdjustmentCommandHandler : IRequestHandler<RejectAttendanceAdjustmentCommand, bool>
{
    private readonly IAttendanceAdjustmentRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public RejectAttendanceAdjustmentCommandHandler(
        IAttendanceAdjustmentRepository repository,
        ICurrentUserService currentUser,
        IClock clock,
        IPublisher publisher)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
        _publisher = publisher;
    }

    public async Task<bool> Handle(RejectAttendanceAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (existing.Status != AdjustmentStatus.Pending)
            throw new DomainValidationException(
                $"Adjustment {request.Id} cannot be rejected — current status is {existing.Status}.");

        if (existing.CreatedByUserId == userId)
            throw new DomainValidationException("BR-10: Self-review is forbidden.");

        var now = _clock.UtcNow;
        existing.Status = AdjustmentStatus.Rejected;
        existing.ReviewedByUserId = userId;
        existing.ReviewedAt = now;
        existing.ReviewNote = request.ReviewNote;
        existing.UpdatedAt = now;
        existing.UpdatedByUserId = userId;

        var updated = await _repository.UpdateAsync(existing, cancellationToken);

        if (updated)
        {
            await _publisher.Publish(new AttendanceRequestDecidedNotification(
                Kind: AttendanceRequestKind.Adjustment,
                RequestId: existing.Id,
                Decision: AttendanceRequestDecision.Rejected,
                DecidedByUserId: userId,
                SubmittedByUserId: existing.CreatedByUserId,
                RequesterUserId: existing.CreatedByUserId,
                Summary: $"Chỉnh công AttendanceRecord #{existing.AttendanceRecordId}",
                Reason: request.ReviewNote), cancellationToken);
        }

        return updated;
    }
}
