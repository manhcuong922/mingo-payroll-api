using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Common;
using MPS.Application.Features.Attendance.OvertimeRequest.Commands;
using MPS.Application.Features.Attendance.WfhRequest.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.WfhRequest.Commands;

public class ApproveWfhRequestCommandHandler : IRequestHandler<ApproveWfhRequestCommand, bool>
{
    private readonly IWfhRequestRepository _repository;
    private readonly WfhQuotaChecker _quotaChecker;
    private readonly IAttendanceEditLockChecker _editLockChecker;
    private readonly ApproverDepartmentGuard _departmentGuard;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public ApproveWfhRequestCommandHandler(
        IWfhRequestRepository repository,
        WfhQuotaChecker quotaChecker,
        IAttendanceEditLockChecker editLockChecker,
        ApproverDepartmentGuard departmentGuard,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _quotaChecker = quotaChecker;
        _editLockChecker = editLockChecker;
        _departmentGuard = departmentGuard;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(ApproveWfhRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (existing.Status != WfhRequestStatus.Pending)
            throw new DomainValidationException(
                $"WFH request {request.Id} cannot be approved — current status is {existing.Status}.");

        if (existing.CreatedByUserId == userId)
            throw new DomainValidationException("BR-10: Self-approval is forbidden.");

        ApproveOvertimeRequestCommandHandler.EnsureApproverRouteMatches(existing.ApproverRoute, _currentUser.RoleId);

        // DM (Planning Manager) chỉ duyệt được đơn của nhân viên cùng phòng ban; HR duyệt cross-department.
        if (existing.ApproverRoute == OvertimeApproverRoute.DepartmentManager)
        {
            await _departmentGuard.EnsureCanApproveAsync(
                userId, _currentUser.RoleId, existing.EmployeeInformationId, cancellationToken);
        }

        await _editLockChecker.EnsureCanModifyAsync(existing.StartDate, request.OverrideReason, cancellationToken);

        // Re-check quota tại thời điểm approve (race-safe).
        await _quotaChecker.EnsureWithinQuotaAsync(
            existing.EmployeeInformationId,
            existing.StartDate,
            existing.EndDate,
            existing.TotalDays,
            _clock.UtcNow,
            cancellationToken);

        var now = _clock.UtcNow;
        existing.Status = WfhRequestStatus.Approved;
        existing.ReviewedByUserId = userId;
        existing.ReviewedAt = now;
        existing.ReviewNote = request.ReviewNote;
        existing.UpdatedAt = now;
        existing.UpdatedByUserId = userId;

        return await _repository.UpdateAsync(existing, cancellationToken);
    }
}
