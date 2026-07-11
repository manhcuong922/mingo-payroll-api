using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

public class ApproveLeaveRequestCommandHandler : IRequestHandler<ApproveLeaveRequestCommand, bool>
{
    private const string SickLeaveCode = "SICK";
    private const string MaternityLeaveCode = "MATERNITY";

    private readonly ILeaveRequestRepository _repository;
    private readonly IAttendanceEditLockChecker _editLockChecker;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public ApproveLeaveRequestCommandHandler(
        ILeaveRequestRepository repository,
        IAttendanceEditLockChecker editLockChecker,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _editLockChecker = editLockChecker;
        _currentUser = currentUser;
        _clock = clock;
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

        return await _repository.ApproveAndApplyAsync(
            request.Id, userId, request.ReviewNote, _clock.UtcNow, cancellationToken);
    }
}
