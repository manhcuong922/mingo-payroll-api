namespace MPS.Domain.Entities.Compliance;

public class SocialInsuranceRateVersion
{
    public int Id { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsArchived { get; set; } = false;

    public decimal EmployeeSocialPercent { get; set; }
    public decimal EmployerSocialPercent { get; set; }
    // Phần TNLĐ-BNN tách riêng trong EmployerSocialPercent — kế toán cần tách chi phí
    public decimal EmployerOccupationalDiseasePercent { get; set; }

    public decimal EmployeeHealthPercent { get; set; }
    public decimal EmployerHealthPercent { get; set; }

    public decimal EmployeeUnemploymentPercent { get; set; }
    public decimal EmployerUnemploymentPercent { get; set; }

    public decimal EmployerUnionFundPercent { get; set; }

    // "Mức tham chiếu" theo Luật BHXH 2024 (trước đây gọi là "lương cơ sở")
    public decimal ReferenceAmount { get; set; }
    // Trần đóng BHXH/BHYT = SocialCapMultiplier × ReferenceAmount
    public int SocialCapMultiplier { get; set; } = 20;

    public string LegalReference { get; set; } = string.Empty;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
