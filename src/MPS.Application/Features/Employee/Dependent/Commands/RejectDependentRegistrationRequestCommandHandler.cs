using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Notifications;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Employee;
using MPS.Domain.Enums.Notifications;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Employee;

namespace MPS.Application.Features.Employee.Dependent.Commands;

public class RejectDependentRegistrationRequestCommandHandler : IRequestHandler<RejectDependentRegistrationRequestCommand, bool>
{
    private readonly IDependentRegistrationRequestRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public RejectDependentRegistrationRequestCommandHandler(
        IDependentRegistrationRequestRepository repository,
        ICurrentUserService currentUser,
        IClock clock,
        IPublisher publisher)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
        _publisher = publisher;
    }

    public async Task<bool> Handle(RejectDependentRegistrationRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        if (_currentUser.RoleId != SystemRoles.HrId && _currentUser.RoleId != SystemRoles.SystemAdminId)
            throw new ForbiddenAccessException("Chỉ HR/Admin được từ chối đơn đăng ký người phụ thuộc.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (existing.Status != DependentRequestStatus.Pending)
            throw new DomainValidationException(
                $"Dependent request {request.Id} cannot be rejected — current status is {existing.Status}.");

        if (existing.CreatedByUserId == userId)
            throw new DomainValidationException("BR-10: Self-review is forbidden.");

        var now = _clock.UtcNow;
        existing.Status = DependentRequestStatus.Rejected;
        existing.ReviewedByUserId = userId;
        existing.ReviewedAt = now;
        existing.ReviewNote = request.ReviewNote;
        existing.UpdatedAt = now;
        existing.UpdatedByUserId = userId;

        var updated = await _repository.UpdateAsync(existing, cancellationToken);

        if (updated)
        {
            await _publisher.Publish(new AttendanceRequestDecidedNotification(
                Kind: AttendanceRequestKind.Dependent,
                RequestId: existing.Id,
                Decision: AttendanceRequestDecision.Rejected,
                DecidedByUserId: userId,
                SubmittedByUserId: existing.CreatedByUserId,
                RequesterUserId: existing.CreatedByUserId,
                Summary: $"Đơn NPT ({existing.RequestType}): {existing.FullName}",
                Reason: request.ReviewNote), cancellationToken);
        }

        return updated;
    }
}
