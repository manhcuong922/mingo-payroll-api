using MPS.Domain.Attributes;

namespace MPS.Domain.Entities.Payroll.BhxhTax;

/// <summary>
/// 1 ảnh chứng từ (biên lai, xác nhận cơ quan, ...) gắn với 1 <see cref="PaymentObligation"/>.
/// Immutable — chỉ create/delete, không update metadata. Cho phép upload nhiều ảnh trong quá
/// trình đóng lô cho đến khi obligation cha chuyển <c>Completed</c>.
/// </summary>
[Auditable]
public class PaymentObligationEvidence
{
    public int Id { get; set; }

    public int PaymentObligationId { get; set; }
    public PaymentObligation PaymentObligation { get; set; } = null!;

    /// <summary>MinIO object key (JPG/PNG/WEBP).</summary>
    public string ObjectKey { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    /// <summary>User note khi upload (VD "Biên lai đợt 1 gồm NV 001-020").</summary>
    public string? Description { get; set; }

    public int UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;
    public DateTime UploadedAt { get; set; }
}
