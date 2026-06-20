namespace MPS.Domain.Entities.Compliance.AutoUpdate;

/// <summary>
/// "Văn bản đang chi phối" cho mỗi loại entity compliance — móng cho Kênh B (watcher chủ động):
/// watcher iterate bảng này để biết PHẢI kiểm tra cái gì và đối chiếu với văn bản nguồn nào.
/// Được upsert mỗi khi một proposal được duyệt thành version mới.
/// </summary>
public sealed class ComplianceAuthoritativeSource
{
    public int Id { get; set; }

    /// <summary>Xem <see cref="TargetEntityTypes"/>. UNIQUE — mỗi loại entity một bản ghi.</summary>
    public string TargetEntityType { get; set; } = string.Empty;

    /// <summary>LegalDocument đang chi phối (nếu đã link).</summary>
    public int? LegalDocumentId { get; set; }
    public string? DocumentNo { get; set; }
    public string? DocumentUrl { get; set; }

    /// <summary>Lần gần nhất xác nhận data active còn khớp văn bản nguồn (Kênh B cập nhật).</summary>
    public DateTime? LastVerifiedAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}
