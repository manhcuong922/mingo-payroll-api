namespace MPS.Domain.Entities.Compliance;

public sealed class LegalDocument
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public DateOnly IssuedDate { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? Url { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    public ICollection<ComplianceVersionLegalDocument> ComplianceVersions { get; set; } = [];
}
