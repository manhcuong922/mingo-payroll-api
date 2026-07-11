using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Notifications;
using MPS.Application.Common.Models.Storage;
using MPS.Application.Common.Storage;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using CompanyPolicyFileEntity = MPS.Domain.Entities.CompanyPolicyFile;

namespace MPS.Application.Features.CompanyPolicy.Commands;

public sealed record UploadCompanyPolicyVersionCommand(
    int PolicyId,
    Stream FileStream,
    string FileName,
    long FileSizeBytes,
    string? Notes) : IRequest<int>;

public sealed class UploadCompanyPolicyVersionCommandHandler
    : IRequestHandler<UploadCompanyPolicyVersionCommand, int>
{
    private readonly ICompanyPolicyRepository _repo;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;

    public UploadCompanyPolicyVersionCommandHandler(
        ICompanyPolicyRepository repo,
        IFileStorageService storage,
        ICurrentUserService currentUser,
        IPublisher publisher)
    {
        _repo = repo;
        _storage = storage;
        _currentUser = currentUser;
        _publisher = publisher;
    }

    public async Task<int> Handle(UploadCompanyPolicyVersionCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new DomainValidationException("UPLOAD_REQUIRES_AUTHENTICATION");

        var policy = await _repo.GetByIdWithFilesAsync(request.PolicyId, cancellationToken)
            ?? throw new DomainValidationException($"COMPANY_POLICY_NOT_FOUND:{request.PolicyId}");

        var detected = await CompanyPolicyFileValidator.ValidateAsync(
            request.FileStream, request.FileName, request.FileSizeBytes, cancellationToken);

        var nextVersion = policy.Files.Count == 0 ? 1 : policy.Files.Max(f => f.Version) + 1;
        var objectKey = ObjectKeyBuilder.ForCompanyPolicy(policy.Id, nextVersion, detected.Extension);

        var stored = await _storage.UploadAsync(new FileUploadRequest(
            Content: request.FileStream,
            ObjectKey: objectKey,
            ContentType: detected.MimeType,
            KnownSizeBytes: request.FileSizeBytes), cancellationToken);

        var now = DateTime.UtcNow;
        var fileId = await _repo.AddFileAsCurrentAsync(policy.Id, new CompanyPolicyFileEntity
        {
            FileName = request.FileName,
            MinioBucket = stored.Bucket,
            MinioObjectKey = objectKey,
            MimeType = detected.MimeType,
            FileSizeBytes = stored.SizeBytes,
            Sha256 = stored.Sha256Hex ?? string.Empty,
            Notes = request.Notes?.Trim(),
            UploadedAt = now,
            UploadedByUserId = userId,
        }, cancellationToken);

        // Touch policy's UpdatedAt so list sort reflects the new version.
        await _repo.UpdateMetadataAsync(policy.Id, policy.Title, policy.Description,
            policy.IsActive, userId, now, cancellationToken);

        await _publisher.Publish(
            new CompanyPolicyChangedNotification(policy.Id, policy.Title, CompanyPolicyChangeKind.NewVersion),
            cancellationToken);

        return fileId;
    }
}
