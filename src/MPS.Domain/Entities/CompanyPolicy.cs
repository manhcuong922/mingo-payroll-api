namespace MPS.Domain.Entities;

/// <summary>
/// Tài liệu nội quy công ty. Một policy có nhiều version file (PDF/DOCX) qua
/// CompanyPolicyFiles; version mới upload đè IsCurrent của version cũ → toàn bộ
/// NV thấy version active mới.
/// </summary>
public class CompanyPolicy
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// false ⇒ ẩn khỏi danh sách NV (soft-disable). File vẫn lưu trong MinIO để
    /// có thể activate lại; chỉ DeleteCommand mới xoá hẳn.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    public ICollection<CompanyPolicyFile> Files { get; set; } = new List<CompanyPolicyFile>();
}
