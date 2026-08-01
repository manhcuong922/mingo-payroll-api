using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Notifications;
using MPS.Application.Features.Attendance.Common;
using MPS.Application.Features.Attendance.OvertimeRequest.Commands;
using MPS.Application.Features.Attendance.WfhRequest.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Enums.Notifications;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Attendance.WfhRequest.Commands;

public class ApproveWfhRequestCommandHandler : IRequestHandler<ApproveWfhRequestCommand, bool>
{
    private readonly IWfhRequestRepository _repository;
    private readonly WfhQuotaChecker _quotaChecker;
    private readonly IAttendanceEditLockChecker _editLockChecker;
    private readonly ApproverDepartmentGuard _departmentGuard;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPayrollSourceChangeHandler _payrollChange;
    private readonly IPublisher _publisher;

    public ApproveWfhRequestCommandHandler(
        IWfhRequestRepository repository,
        WfhQuotaChecker quotaChecker,
        IAttendanceEditLockChecker editLockChecker,
        ApproverDepartmentGuard departmentGuard,
        ICurrentUserService currentUser,
        IClock clock,
        IPayrollSourceChangeHandler payrollChange,
        IPublisher publisher)
    {
        _repository = repository;
        _quotaChecker = quotaChecker;
        _editLockChecker = editLockChecker;
        _departmentGuard = departmentGuard;
        _currentUser = currentUser;
        _clock = clock;
        _payrollChange = payrollChange;
        _publisher = publisher;
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

        // BS-4 / [D11]: duyệt + sinh AttendanceRecord (Present, đủ giờ ca) trong cùng transaction.
        // Trước đây chỉ đổi Status ⇒ ngày WFH không có bản ghi chấm công nào ⇒ mất công khi tính lương.
        var applied = await _repository.ApproveAndApplyAsync(
            request.Id, userId, request.ReviewNote, _clock.UtcNow, cancellationToken);

        // BS-6 + BS-5: WFH sinh công → mark stale kỳ Draft, capture Finalized cho NV.
        if (applied)
            await _payrollChange.OnSourceChangedAsync(
                MonthsInRange(existing.StartDate, existing.EndDate),
                new[] { existing.EmployeeInformationId },
                $"Duyệt WFH {existing.StartDate:dd/MM/yyyy}–{existing.EndDate:dd/MM/yyyy}", cancellationToken);

        if (applied)
        {
            await _publisher.Publish(new AttendanceRequestDecidedNotification(
                Kind: AttendanceRequestKind.Wfh,
                RequestId: existing.Id,
                Decision: AttendanceRequestDecision.Approved,
                DecidedByUserId: userId,
                SubmittedByUserId: existing.CreatedByUserId,
                RequesterUserId: existing.CreatedByUserId,
                Summary: $"WFH {existing.StartDate:dd/MM/yyyy}–{existing.EndDate:dd/MM/yyyy}",
                Reason: request.ReviewNote), cancellationToken);
        }

        return applied;
    }

    private static IEnumerable<(int Year, int Month)> MonthsInRange(DateOnly start, DateOnly end)
    {
        var seen = new HashSet<(int, int)>();
        for (var d = start; d <= end; d = d.AddDays(1))
            seen.Add((d.Year, d.Month));
        return seen;
    }
}
