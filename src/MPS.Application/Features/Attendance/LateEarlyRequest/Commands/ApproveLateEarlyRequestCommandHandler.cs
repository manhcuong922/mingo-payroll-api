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
    private readonly IAttendanceEditLockChecker _editLockChecker;
    private readonly ApproverDepartmentGuard _departmentGuard;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly LateEarlyAttendanceDock _attendanceDock;
    private readonly IPayrollSourceChangeHandler _payrollChange;
    private readonly IPublisher _publisher;

    public ApproveLateEarlyRequestCommandHandler(
        ILateEarlyRequestRepository repository,
        IAttendanceEditLockChecker editLockChecker,
        ApproverDepartmentGuard departmentGuard,
        ICurrentUserService currentUser,
        IClock clock,
        LateEarlyAttendanceDock attendanceDock,
        IPayrollSourceChangeHandler payrollChange,
        IPublisher publisher)
    {
        _attendanceDock = attendanceDock;
        _payrollChange = payrollChange;
        _repository = repository;
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

        // ⭐ LATE-EARLY-2026-08 — BỎ quota nội quy (2 lần/tháng, ≤30 phút/lần) và BỎ luôn đường trừ
        // quỹ phép năm đi kèm nó (quyết định PO 24/08/2026). NLĐ nộp đơn không giới hạn số lần; cái
        // được siết lại là PHẠM VI: đơn không được vượt thời lượng ca được phân của ngày (chặn ở
        // `CreateLateEarlyRequestCommandHandler`).
        //
        // Vì sao bỏ hẳn chứ không nới hạn mức: hai chế tài chồng lên nhau. Đơn được duyệt thì ngày đó
        // đã hưởng TRỌN công của ca (xem `PayrollDataCollector.DayWeight`), trừ thêm quỹ phép là phạt
        // lần thứ hai cho cùng một sự việc — và phạt bằng NGÀY PHÉP, thứ không liên quan gì tới việc
        // đi muộn. Cột `DeductFromLeaveBalance` giữ lại trên entity để đọc lịch sử đơn cũ, từ nay
        // luôn `false`.
        var now = _clock.UtcNow;
        existing.Status = LateEarlyRequestStatus.Approved;
        existing.DeductFromLeaveBalance = false;
        existing.ReviewedByUserId = userId;
        existing.ReviewedAt = now;
        existing.ReviewNote = request.ReviewNote;
        existing.UpdatedAt = now;
        existing.UpdatedByUserId = userId;

        // Lưu Approved TRƯỚC khi recompute để LateEarlyExcuseResolver đọc đúng tổng phút miễn.
        await _repository.UpdateAsync(existing, cancellationToken);

        // Cộng trả giờ công đã bị mất do trễ/sớm bằng cách recompute DayHours từ CheckIn/Out +
        // shift + tổng phút miễn. Giờ công (`DayHours`) vẫn phải đúng vì nó là căn cứ của trần OT,
        // bậc hỗ trợ HĐ khoán và các báo cáo chấm công — ngay cả khi CÔNG của ngày đã được chốt
        // trọn 1 ca bởi `PayrollDataCollector.DayWeight`.
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

        return new ApproveLateEarlyRequestResult(Approved: true);
    }
}
