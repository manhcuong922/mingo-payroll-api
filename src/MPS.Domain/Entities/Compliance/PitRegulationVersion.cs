using MPS.Domain.Interfaces;

namespace MPS.Domain.Entities.Compliance;

public sealed class PitRegulationVersion : IComplianceVersion
{
    public int Id { get; set; }
    public DateOnly ValidFrom { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }

    public decimal PersonalDeductionAmount { get; set; }
    public decimal DependentDeductionAmount { get; set; }

    public int ShortTermContractMaxMonths { get; set; } = 3;
    public decimal ShortTermFlatRatePercent { get; set; } = 10.00m;
    public decimal ShortTermTaxExemptMonthlyIncome { get; set; } = 2_000_000m;

    public string LegalReference { get; set; } = string.Empty;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    public ICollection<PitBracket> Brackets { get; set; } = [];
}
