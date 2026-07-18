using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.LeaveRequest.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

public class UploadLeaveMedicalCertificateCommandHandler : IRequestHandler<UploadLeaveMedicalCertificateCommand, bool>
{
    private readonly ILeaveRequestRepository _repository;
    private readonly IAttendanceEditLockChecker _editLockChecker;
    private readonly MedicalCertificateUploader _certificateUploader;
    private readonly IPermissionResolver _permissionResolver;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public UploadLeaveMedicalCertificateCommandHandler(
        ILeaveRequestRepository repository,
        IAttendanceEditLockChecker editLockChecker,
        MedicalCertificateUploader certificateUploader,
        IPermissionResolver permissionResolver,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _editLockChecker = editLockChecker;
        _certificateUploader = certificateUploader;
        _permissionResolver = permissionResolver;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(UploadLeaveMedicalCertificateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var roleId = _currentUser.RoleId
            ?? throw new ForbiddenAccessException("Role context is required.");

        var existing = await _repository.GetByIdAsync(request.LeaveRequestId, cancellationToken);
        if (existing is null) return false;

        if (existing.Status != LeaveRequestStatus.Pending)
            throw new DomainValidationException(
                "Chỉ có thể upload chứng nhận y tế khi đơn đang chờ duyệt.");

        var isOwner = existing.CreatedByUserId == userId;
        if (!isOwner)
        {
            var permissions = await _permissionResolver.GetPermissionsForRoleAsync(roleId, cancellationToken);
            if (!permissions.Contains(Permissions.LeaveRequestApproveAll))
                throw new ForbiddenAccessException(
                    "Bạn không có quyền upload chứng nhận y tế cho đơn này.");
        }

        await _editLockChecker.EnsureCanModifyAsync(existing.StartDate, request.OverrideReason, cancellationToken);

        var newObjectKey = await _certificateUploader.UploadAsync(
            existing.EmployeeInformationId,
            request.Content,
            request.ContentType,
            request.SizeBytes,
            cancellationToken);

        var oldObjectKey = existing.MedicalCertificateObjectKey;
        existing.MedicalCertificateObjectKey = newObjectKey;
        existing.UpdatedAt = _clock.UtcNow;
        existing.UpdatedByUserId = userId;

        try
        {
            await _repository.UpdateAsync(existing, cancellationToken);
        }
        catch
        {
            await _certificateUploader.TryDeleteAsync(newObjectKey, CancellationToken.None);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(oldObjectKey)
            && !string.Equals(oldObjectKey, newObjectKey, StringComparison.Ordinal))
        {
            await _certificateUploader.TryDeleteAsync(oldObjectKey, CancellationToken.None);
        }

        return true;
    }
}
