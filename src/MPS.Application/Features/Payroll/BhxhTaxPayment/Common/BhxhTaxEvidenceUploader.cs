using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Storage;
using MPS.Application.Common.Storage;
using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Common;

/// <summary>Loại ảnh evidence cho nghĩa vụ nộp BHXH/TNCN.</summary>
public enum BhxhTaxEvidenceKind
{
    /// <summary>Ảnh biên lai chuyển khoản ngân hàng.</summary>
    BankReceipt = 1,
    /// <summary>Ảnh xác nhận / email từ cơ quan BHXH hoặc Thuế.</summary>
    AuthorityConfirmation = 2,
}

/// <summary>
/// Upload ảnh evidence (biên lai bank / xác nhận cơ quan) cho <c>PaymentObligation</c> lên MinIO.
/// Validate content-type + size, build object key, wrap storage errors thành <c>DomainValidationException</c>
/// tiếng Việt. Chỉ chấp nhận JPG/PNG/WEBP, ≤5MB. Caller có trách nhiệm delete object khi bước persist fail.
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
        BhxhTaxEvidenceKind kind,
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

        var objectKey = ObjectKeyBuilder.ForBhxhTaxEvidence(
            obligationId, KindToSegment(kind), contentType, Guid.NewGuid());

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
                "Upload BHXH/Tax evidence to MinIO failed. ObligationId={ObligationId}, Kind={Kind}, ObjectKey={ObjectKey}.",
                obligationId, kind, objectKey);
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

    private static string KindToSegment(BhxhTaxEvidenceKind kind) => kind switch
    {
        BhxhTaxEvidenceKind.BankReceipt => "bank-receipt",
        BhxhTaxEvidenceKind.AuthorityConfirmation => "authority-confirmation",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
