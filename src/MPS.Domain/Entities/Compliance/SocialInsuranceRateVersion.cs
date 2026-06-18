using MPS.Domain.Attributes;
using MPS.Domain.Interfaces;

namespace MPS.Domain.Entities.Compliance;

[Auditable]
public sealed class SocialInsuranceRateVersion : IComplianceVersion
{
    public int Id { get; set; }
    public DateOnly ValidFrom { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }

    public decimal EmployeeSocialPercent { get; set; }
    public decimal EmployerSocialPercent { get; set; }
    public decimal EmployerOccupationalDiseasePercent { get; set; }
    public decimal EmployeeHealthPercent { get; set; }
    public decimal EmployerHealthPercent { get; set; }
    public decimal EmployeeUnemploymentPercent { get; set; }
    public decimal EmployerUnemploymentPercent { get; set; }
    public decimal EmployerUnionFundPercent { get; set; }
    public decimal ReferenceAmount { get; set; }
    public int SocialCapMultiplier { get; set; } = 20;
    public int UnemploymentCapMultiplier { get; set; } = 20;

    public string LegalReference { get; set; } = string.Empty;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
