using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Commands;

public class RejectAttendanceAdjustmentCommandHandler : IRequestHandler<RejectAttendanceAdjustmentCommand, bool>
{
    private readonly IAttendanceAdjustmentRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public RejectAttendanceAdjustmentCommandHandler(
        IAttendanceAdjustmentRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
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

        return await _repository.UpdateAsync(existing, cancellationToken);
    }
}
