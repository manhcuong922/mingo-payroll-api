namespace MPS.Domain.Entities.Compliance;

public sealed class ComplianceVersionLegalDocument
{
    public int Id { get; set; }

    // Polymorphic FK — enforce ở Application layer, không enforce cứng ở DB
    public string ComplianceEntityType { get; set; } = string.Empty;
    public int ComplianceVersionId { get; set; }

    public int LegalDocumentId { get; set; }
    public LegalDocument LegalDocument { get; set; } = null!;

    public DateTime LinkedAt { get; set; }
    public int LinkedByUserId { get; set; }
    public User LinkedByUser { get; set; } = null!;
}
