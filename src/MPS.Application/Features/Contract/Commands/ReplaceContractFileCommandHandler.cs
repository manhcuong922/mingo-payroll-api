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

public class ReplaceContractFileCommandHandler : IRequestHandler<ReplaceContractFileCommand, int>
{
    private readonly IContractRepository _repository;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ReplaceContractFileCommandHandler> _logger;

    public ReplaceContractFileCommandHandler(
        IContractRepository repository,
        IFileStorageService storage,
        ICurrentUserService currentUser,
        ILogger<ReplaceContractFileCommandHandler> logger)
    {
        _repository = repository;
        _storage = storage;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<int> Handle(ReplaceContractFileCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required to replace a contract file.");

        var contract = await _repository.GetByIdAsync(request.ContractId, includeAllowances: false, cancellationToken)
            ?? throw new DomainValidationException($"Contract {request.ContractId} not found.");

        if (contract.ArchivedAt is not null)
            throw new DomainValidationException($"CONTRACT_ARCHIVED: Contract {request.ContractId} is archived and read-only.");

        await PdfStreamValidator.ValidateAsync(
            request.PdfStream, request.FileSizeBytes, request.ContentType, cancellationToken: cancellationToken);

        // Compute next version. Reading current file here is best-effort; the repo also
        // reads-and-bumps inside the transaction (single source of truth for ordering).
        var current = await _repository.GetCurrentFileAsync(
            request.ContractId, FileTargetType.Contract, cancellationToken);
        var nextVersion = (current?.Version ?? 0) + 1;
        var objectKey = ObjectKeyBuilder.ForContract(contract.ContractNo, nextVersion);

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
                "MinIO upload failed when replacing file for contract {ContractId} ({ObjectKey}).",
                request.ContractId, objectKey);
            throw;
        }

        var newFile = new ContractFile
        {
            FileName = request.FileName,
            MinioBucket = metadata.Bucket,
            MinioObjectKey = metadata.ObjectKey,
            CdnUrl = string.Empty,
            MimeType = request.ContentType,
            FileSizeBytes = metadata.SizeBytes,
            Sha256 = metadata.Sha256Hex,
            SignedBySource = request.SignedBySource,
            UploadedAt = metadata.UploadedAtUtc,
            UploadedByUserId = userId,
        };

        try
        {
            return await _repository.ReplaceCurrentFileAsync(
                request.ContractId, FileTargetType.Contract, newFile, cancellationToken);
        }
        catch
        {
            try
            {
                await _storage.DeleteAsync(metadata.ObjectKey, metadata.Bucket, CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx,
                    "Failed to clean up orphaned MinIO object {ObjectKey} after replace DB error for contract {ContractId}.",
                    metadata.ObjectKey, request.ContractId);
            }
            throw;
        }
    }
}
