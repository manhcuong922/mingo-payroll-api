using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Storage;
using MPS.Application.Common.Storage;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractTemplate.Commands;

public sealed record UploadContractTemplateCommand(
    int TemplateId,
    Stream FileStream,
    string FileName,
    long FileSizeBytes) : IRequest<int>;

public sealed class UploadContractTemplateCommandHandler : IRequestHandler<UploadContractTemplateCommand, int>
{
    private static readonly byte[] DocxMagicBytes = [0x50, 0x4B, 0x03, 0x04]; // PK zip

    private readonly IContractTemplateRepository _templateRepository;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;

    public UploadContractTemplateCommandHandler(
        IContractTemplateRepository templateRepository,
        IFileStorageService storage,
        ICurrentUserService currentUser)
    {
        _templateRepository = templateRepository;
        _storage = storage;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(UploadContractTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _templateRepository.GetByIdAsync(request.TemplateId, cancellationToken)
            ?? throw new DomainValidationException($"TEMPLATE_NOT_FOUND: ContractTemplate {request.TemplateId} does not exist.");

        if (request.FileSizeBytes > 5_242_880)
            throw new DomainValidationException("TEMPLATE_FILE_TOO_LARGE: Maximum template file size is 5MB.");

        await ValidateDocxMagicBytesAsync(request.FileStream, cancellationToken);

        var nextVersion = template.Version + 1;
        var objectKey = ObjectKeyBuilder.ForTemplate(request.TemplateId, nextVersion);

        var stored = await _storage.UploadAsync(new FileUploadRequest(
            Content: request.FileStream,
            ObjectKey: objectKey,
            ContentType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            KnownSizeBytes: request.FileSizeBytes), cancellationToken);

        var uploadedByUserId = _currentUser.UserId
            ?? throw new DomainValidationException("UPLOAD_REQUIRES_AUTHENTICATION: User identity could not be resolved.");

        var file = new ContractFile
        {
            TargetType = FileTargetType.Template,
            TargetId = request.TemplateId,
            FileName = request.FileName,
            MinioBucket = stored.Bucket,
            MinioObjectKey = objectKey,
            CdnUrl = string.Empty,
            MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileSizeBytes = request.FileSizeBytes,
            Sha256 = stored.Sha256Hex ?? string.Empty,
            Version = nextVersion,
            IsCurrent = true,
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = uploadedByUserId,
        };

        return await _templateRepository.AttachTemplateFileAsync(request.TemplateId, file, cancellationToken);
    }

    private static async Task ValidateDocxMagicBytesAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[4];
        var read = await stream.ReadAsync(header, ct);
        stream.Seek(0, SeekOrigin.Begin);

        if (read < 4 || !header.SequenceEqual(DocxMagicBytes))
            throw new DomainValidationException("INVALID_DOCX: File must be a valid DOCX (ZIP-based) document.");
    }
}
