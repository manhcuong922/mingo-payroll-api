namespace MPS.Domain.Entities;

public class ContractFile
{
    public int Id { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public int TargetId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MinioBucket { get; set; } = string.Empty;
    public string MinioObjectKey { get; set; } = string.Empty;
    public string CdnUrl { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/pdf";
    public long FileSizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool IsCurrent { get; set; } = true;
    public string? SignedBySource { get; set; }
    public DateTime UploadedAt { get; set; }
    public int UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;

    public ICollection<ContractTermination> TerminationNotices { get; set; } = new List<ContractTermination>();
}
