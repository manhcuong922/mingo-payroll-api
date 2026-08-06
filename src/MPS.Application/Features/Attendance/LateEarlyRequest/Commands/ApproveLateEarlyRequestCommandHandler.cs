using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Notifications;
using MPS.Application.Features.Attendance.Common;
using MPS.Application.Features.Attendance.LateEarlyRequest.Common;
using MPS.Application.Features.Attendance.OvertimeRequest.Commands;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Enums.Notifications;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Commands;

public class ApproveLateEarlyRequestCommandHandler : IRequestHandler<ApproveLateEarlyRequestCommand, ApproveLateEarlyRequestResult>
{
    private readonly ILateEarlyRequestRepository _repository;
    private readonly LateEarlyLimitChecker _limitChecker;
    private readonly IAttendanceEditLockChecker _editLockChecker;
    private readonly ApproverDepartmentGuard _departmentGuard;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly LateEarlyLeaveDeduction _leaveDeduction;
    private readonly LateEarlyAttendanceDock _attendanceDock;
    private readonly IPayrollSourceChangeHandler _payrollChange;
    private readonly IPublisher _publisher;

    public ApproveLateEarlyRequestCommandHandler(
        ILateEarlyRequestRepository repository,
        LateEarlyLimitChecker limitChecker,
        IAttendanceEditLockChecker editLockChecker,
        ApproverDepartmentGuard departmentGuard,
        ICurrentUserService currentUser,
        IClock clock,
        LateEarlyLeaveDeduction leaveDeduction,
        LateEarlyAttendanceDock attendanceDock,
        IPayrollSourceChangeHandler payrollChange,
        IPublisher publisher)
    {
        _leaveDeduction = leaveDeduction;
        _attendanceDock = attendanceDock;
        _payrollChange = payrollChange;
        _repository = repository;
        _limitChecker = limitChecker;
        _editLockChecker = editLockChecker;
        _departmentGuard = departmentGuard;
        _currentUser = currentUser;
        _clock = clock;
        _publisher = publisher;
    }

    public async Task<ApproveLateEarlyRequestResult> Handle(ApproveLateEarlyRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Late/Early request {request.Id} not found.");

        if (existing.Status != LateEarlyRequestStatus.Pending)
            throw new DomainValidationException(
                $"Late/Early request {request.Id} cannot be approved — current status is {existing.Status}.");

        if (existing.CreatedByUserId == userId)
            throw new DomainValidationException("BR-10: Self-approval is forbidden.");

        ApproveOvertimeRequestCommandHandler.EnsureApproverRouteMatches(existing.ApproverRoute, _currentUser.RoleId);

        // DM (Planning Manager) chỉ duyệt được đơn của nhân viên cùng phòng ban; HR duyệt cross-department.
        if (existing.ApproverRoute == OvertimeApproverRoute.DepartmentManager)
        {
            await _departmentGuard.EnsureCanApproveAsync(
                userId, _currentUser.RoleId, existing.EmployeeInformationId, cancellationToken);
        }

        await _editLockChecker.EnsureCanModifyAsync(existing.WorkDate, request.OverrideReason, cancellationToken);

        var limit = await _limitChecker.CheckAsync(
            existing.EmployeeInformationId,
            existing.WorkDate,
            existing.Kind,
            existing.Minutes,
            _clock.UtcNow,
            cancellationToken);

        if (limit.MustDeductFromLeaveBalance && !request.DeductFromLeaveBalance)
        {
            return new ApproveLateEarlyRequestResult(
                Approved: false,
                MustDeductBecauseQuotaExceeded: true,
                Warning: $"Đã có {limit.ApprovedInMonth}/{limit.MaxOccurrencesPerMonth} đơn Approved trong tháng "
                    + $"hoặc vượt {limit.MaxMinutesPerOccurrence} phút. "
                    + "Approver phải xác nhận trừ vào quỹ phép năm (DeductFromLeaveBalance=true) để duyệt.");
        }

        var now = _clock.UtcNow;
        existing.Status = LateEarlyRequestStatus.Approved;
        existing.DeductFromLeaveBalance = request.DeductFromLeaveBalance;
        existing.ReviewedByUserId = userId;
        existing.ReviewedAt = now;
        existing.ReviewNote = request.ReviewNote;
        existing.UpdatedAt = now;
        existing.UpdatedByUserId = userId;

        // BS-8: cờ deduct phải TRỪ THẬT vào quỹ phép năm. Trừ TRƯỚC khi lưu đơn — nếu quỹ không đủ
        // thì ném lỗi và đơn vẫn ở Pending (không có trạng thái nửa vời "đã duyệt nhưng chưa trừ").
        if (request.DeductFromLeaveBalance)
        {
            await _leaveDeduction.ApplyAsync(
                existing.EmployeeInformationId, existing.WorkDate, existing.Minutes,
                userId, now, cancellationToken);
        }

        // Lưu Approved TRƯỚC khi recompute để LateEarlyExcuseResolver đọc đúng tổng phút miễn.
        await _repository.UpdateAsync(existing, cancellationToken);

        // Cộng trả giờ công đã bị mất do trễ/sớm bằng cách recompute DayHours từ CheckIn/Out +
        // shift + tổng phút miễn. Không phụ thuộc cờ DeductFromLeaveBalance — quy tắc: đơn
        // Approved luôn miễn giờ công, cờ chỉ quyết định có trừ quỹ phép hay không.
        await _attendanceDock.RecomputeAsync(
            existing.EmployeeInformationId, existing.WorkDate,
            existing.Id, userId, now, cancellationToken);

        // BS-6 + BS-5: đổi công/quỹ của ngày → mark stale kỳ Draft, capture Finalized cho NV.
        await _payrollChange.OnSourceChangedAsync(
            existing.WorkDate.Year, existing.WorkDate.Month, existing.EmployeeInformationId,
            $"Duyệt đi muộn/về sớm ngày {existing.WorkDate:dd/MM/yyyy}", cancellationToken);

        await _publisher.Publish(new AttendanceRequestDecidedNotification(
            Kind: AttendanceRequestKind.LateEarly,
            RequestId: existing.Id,
            Decision: AttendanceRequestDecision.Approved,
            DecidedByUserId: userId,
            SubmittedByUserId: existing.CreatedByUserId,
            RequesterUserId: existing.CreatedByUserId,
            Summary: $"{existing.Kind} {existing.Minutes} phút ngày {existing.WorkDate:dd/MM/yyyy}",
            Reason: request.ReviewNote), cancellationToken);

        return new ApproveLateEarlyRequestResult(true, limit.MustDeductFromLeaveBalance, null);
    }
}
