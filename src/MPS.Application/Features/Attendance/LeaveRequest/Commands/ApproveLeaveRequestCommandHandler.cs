using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Common;
using MPS.Application.Features.Attendance.OvertimeRequest.Commands;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

public class ApproveLeaveRequestCommandHandler : IRequestHandler<ApproveLeaveRequestCommand, bool>
{
    private const string SickLeaveCode = "SICK";
    private const string MaternityLeaveCode = "MATERNITY";

    private readonly ILeaveRequestRepository _repository;
    private readonly IAttendanceEditLockChecker _editLockChecker;
    private readonly ApproverDepartmentGuard _departmentGuard;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPayrollStaleMarker _payrollStale;

    public ApproveLeaveRequestCommandHandler(
        ILeaveRequestRepository repository,
        IAttendanceEditLockChecker editLockChecker,
        ApproverDepartmentGuard departmentGuard,
        ICurrentUserService currentUser,
        IClock clock,
        IPayrollStaleMarker payrollStale)
    {
        _repository = repository;
        _editLockChecker = editLockChecker;
        _departmentGuard = departmentGuard;
        _currentUser = currentUser;
        _clock = clock;
        _payrollStale = payrollStale;
    }

    public async Task<bool> Handle(ApproveLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (existing.Status != LeaveRequestStatus.Pending)
            throw new DomainValidationException(
                $"Leave request {request.Id} cannot be approved — current status is {existing.Status}.");

        if (existing.CreatedByUserId == userId)
            throw new DomainValidationException("BR-10: Self-approval is forbidden.");

        // Gate role duyệt theo ApproverRoute của đơn (Hr/DepartmentManager/Director).
        // Legacy record (ApproverRoute = null) giữ hành vi cũ: ai có Approve.All đều duyệt được.
        ApproveOvertimeRequestCommandHandler.EnsureApproverRouteMatches(existing.ApproverRoute, _currentUser.RoleId);

        // Chỉ DM (Planning Manager) cần scope phòng ban; HR/Director duyệt cross-department.
        if (existing.ApproverRoute == OvertimeApproverRoute.DepartmentManager)
        {
            await _departmentGuard.EnsureCanApproveAsync(
                userId, _currentUser.RoleId, existing.EmployeeInformationId, cancellationToken);
        }

        // Cutoff: HR có thể override qua OverrideReason nếu có permission.
        await _editLockChecker.EnsureCanModifyAsync(existing.StartDate, request.OverrideReason, cancellationToken);

        // Fill in / refresh medical cert fields if approver provides them.
        var medicalUrl = string.IsNullOrWhiteSpace(request.MedicalCertificateUrl)
            ? existing.MedicalCertificateUrl
            : request.MedicalCertificateUrl.Trim();
        var insuranceNo = string.IsNullOrWhiteSpace(request.InsuranceCertNumber)
            ? existing.InsuranceCertNumber
            : request.InsuranceCertNumber.Trim();

        if (existing.LeaveType.RequireDocument
            && string.IsNullOrWhiteSpace(existing.SupportingDocumentUrl)
            && string.IsNullOrWhiteSpace(medicalUrl))
        {
            throw new DomainValidationException(
                $"Leave type '{existing.LeaveType.Code}' requires a supporting document before approval.");
        }

        var code = existing.LeaveType.Code?.ToUpperInvariant();
        if ((code == SickLeaveCode || code == MaternityLeaveCode)
            && string.IsNullOrWhiteSpace(insuranceNo))
        {
            throw new DomainValidationException(
                $"Đơn '{existing.LeaveType.Code}' yêu cầu InsuranceCertNumber (số hiệu giấy C65-HD).");
        }

        // Persist any newly-supplied cert data before ApproveAndApply commits balance/attendance.
        if (medicalUrl != existing.MedicalCertificateUrl || insuranceNo != existing.InsuranceCertNumber)
        {
            existing.MedicalCertificateUrl = medicalUrl;
            existing.InsuranceCertNumber = insuranceNo;
            existing.UpdatedAt = _clock.UtcNow;
            existing.UpdatedByUserId = userId;
            await _repository.UpdateAsync(existing, cancellationToken);
        }

        var applied = await _repository.ApproveAndApplyAsync(
            request.Id, userId, request.ReviewNote, _clock.UtcNow, cancellationToken);

        // BS-6: duyệt nghỉ phép sinh AttendanceRecord OnLeave → đổi công → kỳ Draft cần tính lại.
        if (applied)
            await _payrollStale.MarkStaleForMonthsAsync(
                MonthsInRange(existing.StartDate, existing.EndDate),
                $"Duyệt nghỉ phép {existing.StartDate:dd/MM/yyyy}–{existing.EndDate:dd/MM/yyyy}", cancellationToken);

        return applied;
    }

    // Các (năm, tháng) mà khoảng nghỉ vắt qua — 1 đơn có thể trải nhiều tháng.
    private static IEnumerable<(int Year, int Month)> MonthsInRange(DateOnly start, DateOnly end)
    {
        var seen = new HashSet<(int, int)>();
        for (var d = start; d <= end; d = d.AddDays(1))
            seen.Add((d.Year, d.Month));
        return seen;
    }
}
