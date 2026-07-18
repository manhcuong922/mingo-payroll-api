using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Storage;
using MPS.Application.Common.Storage;
using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Attendance.LeaveRequest.Common;

/// <summary>
/// Upload ảnh chứng nhận y tế cho LeaveRequest (SICK/MATERNITY) lên MinIO.
/// Validate content-type + size, build object key, wrap storage errors thành DomainValidationException tiếng Việt.
/// Rollback (delete object) là trách nhiệm của caller nếu bước persist entity fail sau upload.
/// </summary>
public sealed class MedicalCertificateUploader
{
    public const long MaxSizeBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/jpg", "image/png", "image/webp" };

    private readonly IFileStorageService _storage;
    private readonly ILogger<MedicalCertificateUploader> _logger;

    public MedicalCertificateUploader(IFileStorageService storage, ILogger<MedicalCertificateUploader> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// Validate + upload. Trả về objectKey mới (không bao gồm bucket — bucket dùng default của MinIO).
    /// </summary>
    public async Task<string> UploadAsync(
        int employeeInformationId,
        Stream content,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (sizeBytes <= 0)
            throw new DomainValidationException("Ảnh chứng nhận y tế rỗng.");
        if (sizeBytes > MaxSizeBytes)
            throw new DomainValidationException(
                $"Ảnh chứng nhận y tế vượt kích thước tối đa {MaxSizeBytes / (1024 * 1024)} MB.");
        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
            throw new DomainValidationException(
                "Chỉ hỗ trợ ảnh JPG/PNG/WEBP cho chứng nhận y tế.");

        var objectKey = ObjectKeyBuilder.ForLeaveMedicalCertificate(
            employeeInformationId, contentType, Guid.NewGuid());

        var uploadRequest = new FileUploadRequest(
            Content: content,
            ObjectKey: objectKey,
            ContentType: contentType,
            KnownSizeBytes: sizeBytes);

        try
        {
            var metadata = await _storage.UploadAsync(uploadRequest, cancellationToken);
            return metadata.ObjectKey;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Upload medical certificate to MinIO failed for employee {EmployeeId}, objectKey={ObjectKey}.",
                employeeInformationId, objectKey);
            throw new DomainValidationException("Không thể lưu ảnh chứng nhận y tế — vui lòng thử lại.");
        }
    }

    /// <summary>Best-effort delete — log warning và nuốt exception (không throw).</summary>
    public async Task TryDeleteAsync(string? objectKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectKey)) return;
        if (LooksLikeUrl(objectKey)) return; // legacy URL string — không phải object key, bỏ qua.
        try
        {
            await _storage.DeleteAsync(objectKey, bucket: null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Best-effort delete medical certificate MinIO object failed. ObjectKey={ObjectKey}.",
                objectKey);
        }
    }

    public static bool LooksLikeUrl(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
