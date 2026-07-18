using MPS.Domain.Attributes;
using MPS.Domain.Enums;

namespace MPS.Domain.Entities;

/// <summary>
/// Tài liệu / ảnh gắn cho Đơn hoặc Task (v5).
/// Polymorphic qua OwnerType + OwnerId (không FK cứng tới Project/Task) — xóa owner phải dọn ở handler
/// (cả bản ghi DB lẫn object trên MinIO). File vật lý lưu MinIO qua IFileStorageService.
/// </summary>
[Auditable]
public class ProductionAttachment
{
    public int Id { get; set; }

    public AttachmentOwnerType OwnerType { get; set; }     // Project | Task | TaskCompletion
    public int OwnerId { get; set; }                       // ProjectId hoặc TaskId

    public string ObjectKey { get; set; } = string.Empty;  // key trên MinIO
    public string Bucket { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;   // tên gốc hiển thị, max 260
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Sha256Hex { get; set; }
    public string? Note { get; set; }                      // vd "Ảnh nghiệm thu", "Biên bản QC"

    public int UploadedByUserId { get; set; }
    public User? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
}
