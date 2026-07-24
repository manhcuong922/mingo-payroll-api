using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Common;
using MPS.Application.Features.Attendance.LeaveRequest.Common;
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
    private readonly MedicalCertificateUploader _certificateUploader;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPayrollSourceChangeHandler _payrollChange;

    public ApproveLeaveRequestCommandHandler(
        ILeaveRequestRepository repository,
        IAttendanceEditLockChecker editLockChecker,
        ApproverDepartmentGuard departmentGuard,
        MedicalCertificateUploader certificateUploader,
        ICurrentUserService currentUser,
        IClock clock,
        IPayrollSourceChangeHandler payrollChange)
    {
        _repository = repository;
        _editLockChecker = editLockChecker;
        _departmentGuard = departmentGuard;
        _certificateUploader = certificateUploader;
        _currentUser = currentUser;
        _clock = clock;
        _payrollChange = payrollChange;
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

        var insuranceNo = string.IsNullOrWhiteSpace(request.InsuranceCertNumber)
            ? existing.InsuranceCertNumber
            : request.InsuranceCertNumber.Trim();

        string? oldCertObjectKey = existing.MedicalCertificateObjectKey;
        string? newCertObjectKey = null;

        if (request.MedicalCertificateStream is not null)
        {
            newCertObjectKey = await _certificateUploader.UploadAsync(
                existing.EmployeeInformationId,
                request.MedicalCertificateStream,
                request.MedicalCertificateContentType ?? string.Empty,
                request.MedicalCertificateSize ?? 0L,
                cancellationToken);
        }

        var effectiveCertObjectKey = newCertObjectKey ?? existing.MedicalCertificateObjectKey;

        if (existing.LeaveType.RequireDocument && string.IsNullOrWhiteSpace(effectiveCertObjectKey))
        {
            // Rollback upload nếu vừa lỡ tạo (không xảy ra trong flow này vì effectiveCertObjectKey lấy từ new nếu có,
            // nhưng defensive để đối xứng).
            await _certificateUploader.TryDeleteAsync(newCertObjectKey, CancellationToken.None);
            throw new DomainValidationException(
                $"Loại đơn '{existing.LeaveType.Code}' yêu cầu ảnh chứng nhận y tế — vui lòng upload trước khi duyệt.");
        }

        var code = existing.LeaveType.Code?.ToUpperInvariant();
        if ((code == SickLeaveCode || code == MaternityLeaveCode)
            && string.IsNullOrWhiteSpace(insuranceNo))
        {
            await _certificateUploader.TryDeleteAsync(newCertObjectKey, CancellationToken.None);
            throw new DomainValidationException(
                $"Đơn '{existing.LeaveType.Code}' yêu cầu InsuranceCertNumber (số hiệu giấy C65-HD).");
        }

        // Persist any newly-supplied cert data before ApproveAndApply commits balance/attendance.
        if (newCertObjectKey is not null || insuranceNo != existing.InsuranceCertNumber)
        {
            existing.MedicalCertificateObjectKey = effectiveCertObjectKey;
            existing.InsuranceCertNumber = insuranceNo;
            existing.UpdatedAt = _clock.UtcNow;
            existing.UpdatedByUserId = userId;
            try
            {
                await _repository.UpdateAsync(existing, cancellationToken);
            }
            catch
            {
                await _certificateUploader.TryDeleteAsync(newCertObjectKey, CancellationToken.None);
                throw;
            }
        }

        var applied = await _repository.ApproveAndApplyAsync(
            request.Id, userId, request.ReviewNote, _clock.UtcNow, cancellationToken);

        // Thay chứng từ cũ (nhánh dev): giấy cũ chỉ được xoá SAU khi duyệt thành công, và chỉ khi
        // thật sự có giấy mới khác giấy cũ.
        if (applied
            && newCertObjectKey is not null
            && !string.IsNullOrWhiteSpace(oldCertObjectKey)
            && !string.Equals(oldCertObjectKey, newCertObjectKey, StringComparison.Ordinal))
        {
            await _certificateUploader.TryDeleteAsync(oldCertObjectKey, CancellationToken.None);
        }

        // BS-6 + BS-5: duyệt nghỉ phép đổi công → mark stale kỳ Draft, capture Finalized cho NV.
        if (applied)
            await _payrollChange.OnSourceChangedAsync(
                MonthsInRange(existing.StartDate, existing.EndDate),
                new[] { existing.EmployeeInformationId },
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
