using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.LeaveRequest.Common;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LeaveRequest.Queries;

public class GetLeaveMedicalCertificateDownloadUrlQueryHandler
    : IRequestHandler<GetLeaveMedicalCertificateDownloadUrlQuery, LeaveMedicalCertificateDownloadUrlResult?>
{
    private static readonly TimeSpan PresignedTtl = TimeSpan.FromMinutes(15);

    private readonly ILeaveRequestRepository _repository;
    private readonly IFileStorageService _storage;
    private readonly IPermissionResolver _permissionResolver;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public GetLeaveMedicalCertificateDownloadUrlQueryHandler(
        ILeaveRequestRepository repository,
        IFileStorageService storage,
        IPermissionResolver permissionResolver,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _storage = storage;
        _permissionResolver = permissionResolver;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<LeaveMedicalCertificateDownloadUrlResult?> Handle(
        GetLeaveMedicalCertificateDownloadUrlQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var roleId = _currentUser.RoleId
            ?? throw new ForbiddenAccessException("Role context is required.");

        var existing = await _repository.GetByIdAsync(request.LeaveRequestId, cancellationToken);
        if (existing is null) return null;

        var isOwner = existing.CreatedByUserId == userId;
        if (!isOwner)
        {
            var permissions = await _permissionResolver.GetPermissionsForRoleAsync(roleId, cancellationToken);
            if (!permissions.Contains(Permissions.LeaveRequestReadAll))
                throw new ForbiddenAccessException(
                    "Bạn không có quyền xem chứng nhận y tế của đơn này.");
        }

        var stored = existing.MedicalCertificateObjectKey;
        if (string.IsNullOrWhiteSpace(stored)) return null;

        // Backward-compat: bản ghi cũ trước migration có thể lưu URL http/https — trả nguyên.
        if (MedicalCertificateUploader.LooksLikeUrl(stored))
        {
            return new LeaveMedicalCertificateDownloadUrlResult(stored, _clock.UtcNow.Add(PresignedTtl));
        }

        var url = await _storage.GeneratePresignedDownloadUrlAsync(
            stored, PresignedTtl, bucket: null, cancellationToken);
        return new LeaveMedicalCertificateDownloadUrlResult(url, _clock.UtcNow.Add(PresignedTtl));
    }
}
