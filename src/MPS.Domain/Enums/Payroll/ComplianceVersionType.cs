namespace MPS.Domain.Enums.Payroll;

/// <summary>5 loại version compliance mà 1 kỳ lương snapshot lại để tái lập + diff đổi luật.</summary>
public enum ComplianceVersionType
{
    SocialInsurance = 1,
    Pit = 2,
    MinimumWage = 3,
    LaborLawConstants = 4,
    AllowanceThreshold = 5,
}
