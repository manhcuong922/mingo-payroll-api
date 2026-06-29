using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Storage;
using MPS.Application.Common.Storage;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using CompanyPolicyEntity = MPS.Domain.Entities.CompanyPolicy;
using CompanyPolicyFileEntity = MPS.Domain.Entities.CompanyPolicyFile;

namespace MPS.Application.Features.CompanyPolicy.Commands;

public sealed record CreateCompanyPolicyCommand(
    string Title,
    string? Description,
    Stream FileStream,
    string FileName,
    long FileSizeBytes,
    string? Notes) : IRequest<int>;

public sealed class CreateCompanyPolicyCommandHandler
    : IRequestHandler<CreateCompanyPolicyCommand, int>
{
    private readonly ICompanyPolicyRepository _repo;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;

    public CreateCompanyPolicyCommandHandler(
        ICompanyPolicyRepository repo,
        IFileStorageService storage,
        ICurrentUserService currentUser)
    {
        _repo = repo;
        _storage = storage;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreateCompanyPolicyCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new DomainValidationException("UPLOAD_REQUIRES_AUTHENTICATION");

        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainValidationException("COMPANY_POLICY_TITLE_REQUIRED");

        var detected = await CompanyPolicyFileValidator.ValidateAsync(
            request.FileStream, request.FileName, request.FileSizeBytes, cancellationToken);

        var now = DateTime.UtcNow;

        // 1. Insert policy first to get the Id (needed for object key).
        var policyId = await _repo.AddPolicyAsync(new CompanyPolicyEntity
        {
            Title = title,
            Description = request.Description?.Trim(),
            IsActive = true,
            CreatedAt = now,
            CreatedByUserId = userId,
        }, cancellationToken);

        // 2. Upload blob.
        var objectKey = ObjectKeyBuilder.ForCompanyPolicy(policyId, version: 1, detected.Extension);
        var stored = await _storage.UploadAsync(new FileUploadRequest(
            Content: request.FileStream,
            ObjectKey: objectKey,
            ContentType: detected.MimeType,
            KnownSizeBytes: request.FileSizeBytes), cancellationToken);

        // 3. Attach file row.
        await _repo.AddFileAsCurrentAsync(policyId, new CompanyPolicyFileEntity
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

        return policyId;
    }
}
