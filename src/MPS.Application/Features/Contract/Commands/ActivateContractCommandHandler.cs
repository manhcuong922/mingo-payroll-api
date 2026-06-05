using MediatR;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Storage;
using MPS.Application.Common.Storage;
using MPS.Application.Features.Contract.Common;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Contract.Commands;

public class ActivateContractCommandHandler : IRequestHandler<ActivateContractCommand, int>
{
    private readonly IContractRepository _repository;
    private readonly IContractLookupReader _lookup;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ActivateContractCommandHandler> _logger;

    public ActivateContractCommandHandler(
        IContractRepository repository,
        IContractLookupReader lookup,
        IFileStorageService storage,
        ICurrentUserService currentUser,
        ILogger<ActivateContractCommandHandler> logger)
    {
        _repository = repository;
        _lookup = lookup;
        _storage = storage;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<int> Handle(ActivateContractCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required to activate a contract.");

        var contract = await _repository.GetByIdAsync(request.ContractId, includeAllowances: false, cancellationToken)
            ?? throw new DomainValidationException($"Contract {request.ContractId} not found.");

        if (contract.ArchivedAt is not null)
            throw new DomainValidationException($"CONTRACT_ARCHIVED: Contract {request.ContractId} is archived.");

        if (contract.Status != ContractStatus.Draft)
            throw new DomainValidationException(
                $"Cannot activate contract {request.ContractId}: status is {contract.Status}, only Draft can be activated.");

        // PDF gate
        await PdfStreamValidator.ValidateAsync(
            request.PdfStream, request.FileSizeBytes, request.ContentType, cancellationToken: cancellationToken);

        // Re-read SalaryGrade snapshot at activation moment (Plan §7.2 step 5).
        // Frozen value may differ from the one shown when the draft was first created
        // because HR may have updated the grade in between.
        var grade = await _lookup.GetSalaryGradeSnapshotAsync(contract.SalaryGradeId, cancellationToken)
            ?? throw new DomainValidationException($"SalaryGrade {contract.SalaryGradeId} not found at activation time.");

        if (!grade.IsActive)
            throw new DomainValidationException(
                $"SalaryGrade {grade.Id} is no longer active; cannot freeze snapshot. Switch the contract to a current grade first.");

        // Upload to MinIO FIRST so we never mark Active without a persisted file (Plan §14 risk #1).
        var objectKey = ObjectKeyBuilder.ForContract(contract.ContractNo, version: 1);
        var uploadRequest = new FileUploadRequest(
            Content: request.PdfStream,
            ObjectKey: objectKey,
            ContentType: request.ContentType,
            KnownSizeBytes: request.FileSizeBytes);

        StoredFileMetadata metadata;
        try
        {
            metadata = await _storage.UploadAsync(uploadRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "MinIO upload failed for contract {ContractId} ({ObjectKey}); contract remains in Draft.",
                request.ContractId, objectKey);
            throw; // Surface storage exception; middleware maps to 503/500.
        }

        var file = new ContractFile
        {
            // TargetId/TargetType/Version/IsCurrent are set inside ActivateDraftAsync.
            FileName = request.FileName,
            MinioBucket = metadata.Bucket,
            MinioObjectKey = metadata.ObjectKey,
            CdnUrl = string.Empty, // CDN URL is generated on-demand via presigned URLs.
            MimeType = request.ContentType,
            FileSizeBytes = metadata.SizeBytes,
            Sha256 = metadata.Sha256Hex,
            SignedBySource = request.SignedBySource,
            UploadedAt = metadata.UploadedAtUtc,
            UploadedByUserId = userId,
        };

        try
        {
            return await _repository.ActivateDraftAsync(
                contractId: request.ContractId,
                snapshotAmount: grade.BaseSalary,
                snapshotCurrency: grade.Currency,
                signedAtUtc: request.SignedAt,
                signedPlace: request.SignedPlace,
                file: file,
                cancellationToken: cancellationToken);
        }
        catch
        {
            // Compensating delete (best effort): drop the orphaned MinIO blob so storage
            // doesn't accumulate untracked files when DB commit fails.
            try
            {
                await _storage.DeleteAsync(metadata.ObjectKey, metadata.Bucket, CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx,
                    "Failed to clean up orphaned MinIO object {ObjectKey} after activation DB error for contract {ContractId}.",
                    metadata.ObjectKey, request.ContractId);
            }
            throw;
        }
    }
}
