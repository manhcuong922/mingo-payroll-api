using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Storage;
using MPS.Application.Common.Storage;
using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Common;

/// <summary>
/// Upload ảnh chứng từ nộp BHXH/TNCN lên MinIO cho 1 <c>PaymentObligation</c>. v3 model: không
/// còn phân biệt bank-receipt vs authority-confirmation — chỉ là ảnh evidence với description tuỳ ý.
/// Chỉ chấp nhận JPG/PNG/WEBP ≤5MB. Caller có trách nhiệm delete object khi bước persist DB fail.
/// </summary>
public sealed class BhxhTaxEvidenceUploader
{
    public const long MaxSizeBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/jpg", "image/png", "image/webp" };

    private readonly IFileStorageService _storage;
    private readonly ILogger<BhxhTaxEvidenceUploader> _logger;

    public BhxhTaxEvidenceUploader(IFileStorageService storage, ILogger<BhxhTaxEvidenceUploader> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    /// <summary>Validate + upload. Trả về objectKey mới (bucket dùng default).</summary>
    public async Task<string> UploadAsync(
        int obligationId,
        Stream content,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (sizeBytes <= 0)
            throw new DomainValidationException("Ảnh bằng chứng nộp BHXH/Thuế rỗng.");
        if (sizeBytes > MaxSizeBytes)
            throw new DomainValidationException(
                $"Ảnh bằng chứng vượt kích thước tối đa {MaxSizeBytes / (1024 * 1024)} MB.");
        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
            throw new DomainValidationException(
                "Chỉ hỗ trợ ảnh JPG/PNG/WEBP cho bằng chứng nộp BHXH/Thuế.");

        var objectKey = ObjectKeyBuilder.ForBhxhTaxEvidence(obligationId, contentType, Guid.NewGuid());

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
                "Upload BHXH/Tax evidence to MinIO failed. ObligationId={ObligationId}, ObjectKey={ObjectKey}.",
                obligationId, objectKey);
            throw new DomainValidationException("Không thể lưu ảnh bằng chứng — vui lòng thử lại.");
        }
    }

    /// <summary>Best-effort delete — log warning và nuốt exception.</summary>
    public async Task TryDeleteAsync(string? objectKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectKey)) return;
        try
        {
            await _storage.DeleteAsync(objectKey, bucket: null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Best-effort delete BHXH/Tax evidence MinIO object failed. ObjectKey={ObjectKey}.", objectKey);
        }
    }
}
