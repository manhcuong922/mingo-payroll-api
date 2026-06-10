namespace MPS.Domain.Entities.Compliance;

public class PitRegulationVersion
{
    public int Id { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsArchived { get; set; } = false;

    // Giảm trừ bản thân (vd 15,500,000)
    public decimal PersonalDeductionAmount { get; set; }
    // Giảm trừ người phụ thuộc (vd 6,200,000)
    public decimal DependentDeductionAmount { get; set; }

    public string LegalReference { get; set; } = string.Empty;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<PitBracket> Brackets { get; set; } = new List<PitBracket>();
}
