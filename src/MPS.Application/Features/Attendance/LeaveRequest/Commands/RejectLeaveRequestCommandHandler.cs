using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Notifications;
using MPS.Application.Features.Attendance.Common;
using MPS.Application.Features.Attendance.OvertimeRequest.Commands;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Enums.Notifications;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

public class RejectLeaveRequestCommandHandler : IRequestHandler<RejectLeaveRequestCommand, bool>
{
    private readonly ILeaveRequestRepository _repository;
    private readonly ApproverDepartmentGuard _departmentGuard;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public RejectLeaveRequestCommandHandler(
        ILeaveRequestRepository repository,
        ApproverDepartmentGuard departmentGuard,
        ICurrentUserService currentUser,
        IClock clock,
        IPublisher publisher)
    {
        _repository = repository;
        _departmentGuard = departmentGuard;
        _currentUser = currentUser;
        _clock = clock;
        _publisher = publisher;
    }

    public async Task<bool> Handle(RejectLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (existing.Status != LeaveRequestStatus.Pending)
            throw new DomainValidationException(
                $"Leave request {request.Id} cannot be rejected — current status is {existing.Status}.");

        if (existing.CreatedByUserId == userId)
            throw new DomainValidationException("BR-10: Self-review is forbidden.");

        ApproveOvertimeRequestCommandHandler.EnsureApproverRouteMatches(existing.ApproverRoute, _currentUser.RoleId);

        if (existing.ApproverRoute == OvertimeApproverRoute.DepartmentManager)
        {
            await _departmentGuard.EnsureCanApproveAsync(
                userId, _currentUser.RoleId, existing.EmployeeInformationId, cancellationToken);
        }

        var now = _clock.UtcNow;
        existing.Status = LeaveRequestStatus.Rejected;
        existing.ReviewedByUserId = userId;
        existing.ReviewedAt = now;
        existing.ReviewNote = request.ReviewNote;
        existing.UpdatedAt = now;
        existing.UpdatedByUserId = userId;

        var updated = await _repository.UpdateAsync(existing, cancellationToken);

        if (updated)
        {
            // Phase 3 §N2 — báo về người submit (Leave self-service ⇒ submit = requester).
            await _publisher.Publish(new AttendanceRequestDecidedNotification(
                Kind: AttendanceRequestKind.Leave,
                RequestId: request.Id,
                Decision: AttendanceRequestDecision.Rejected,
                DecidedByUserId: userId,
                SubmittedByUserId: existing.CreatedByUserId,
                RequesterUserId: existing.CreatedByUserId,
                Summary: $"Đơn nghỉ {existing.StartDate:dd/MM/yyyy}–{existing.EndDate:dd/MM/yyyy}",
                Reason: request.ReviewNote), cancellationToken);
        }

        return updated;
    }
}
