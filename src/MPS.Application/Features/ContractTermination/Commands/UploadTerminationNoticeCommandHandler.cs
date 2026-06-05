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

namespace MPS.Application.Features.ContractTermination.Commands;

public class UploadTerminationNoticeCommandHandler : IRequestHandler<UploadTerminationNoticeCommand, int>
{
    private readonly IContractRepository _contracts;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UploadTerminationNoticeCommandHandler> _logger;

    public UploadTerminationNoticeCommandHandler(
        IContractRepository contracts,
        IFileStorageService storage,
        ICurrentUserService currentUser,
        ILogger<UploadTerminationNoticeCommandHandler> logger)
    {
        _contracts = contracts;
        _storage = storage;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<int> Handle(UploadTerminationNoticeCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required to upload notice.");

        var contract = await _contracts.GetByIdAsync(request.ContractId, includeAllowances: false, cancellationToken)
            ?? throw new DomainValidationException($"Contract {request.ContractId} not found.");

        if (contract.ArchivedAt is not null)
            throw new DomainValidationException($"CONTRACT_ARCHIVED: Contract {request.ContractId} is archived.");

        if (contract.Status != ContractStatus.Active)
            throw new DomainValidationException(
                $"Cannot upload termination notice: contract {request.ContractId} status is {contract.Status}, expected Active.");

        await PdfStreamValidator.ValidateAsync(
            request.PdfStream, request.FileSizeBytes, request.ContentType, cancellationToken: cancellationToken);

        // Upload MinIO FIRST (same risk-1 pattern as activate). Repository call will
        // demote prior IsCurrent file + bump version inside its own transaction.
        var objectKey = ObjectKeyBuilder.ForTerminationNotice(contract.ContractNo, version: 1);
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
                "MinIO upload failed for termination notice of contract {ContractId} ({ObjectKey}).",
                request.ContractId, objectKey);
            throw;
        }

        var file = new ContractFile
        {
            TargetType = FileTargetType.Termination,
            TargetId = contract.Id, // ContractTermination chưa tồn tại → key về ContractId.
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
            return await _contracts.AddContractFileForActiveContractAsync(contract.Id, file, cancellationToken);
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
                    "Failed to clean up orphaned MinIO object {ObjectKey} for contract {ContractId}.",
                    metadata.ObjectKey, request.ContractId);
            }
            throw;
        }
    }
}
