namespace MPS.Domain.Entities;

/// <summary>
/// Một version file đính kèm policy. Format hỗ trợ: PDF + DOCX.
/// IsCurrent=true ⇒ version đang hiển thị cho NV. Mỗi policy chỉ có tối đa 1
/// file IsCurrent tại 1 thời điểm (enforce bằng filtered unique index).
/// </summary>
public class CompanyPolicyFile
{
    public int Id { get; set; }

    public int CompanyPolicyId { get; set; }
    public CompanyPolicy CompanyPolicy { get; set; } = null!;

    public int Version { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MinioBucket { get; set; } = string.Empty;
    public string MinioObjectKey { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/pdf";
    public long FileSizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public bool IsCurrent { get; set; }

    public DateTime UploadedAt { get; set; }
    public int UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;
}
