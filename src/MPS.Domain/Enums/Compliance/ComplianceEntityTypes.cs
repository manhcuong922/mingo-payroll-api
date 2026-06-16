namespace MPS.Domain.Enums.Compliance;

public static class ComplianceEntityTypes
{
    public const string SocialInsuranceRate = "SocialInsuranceRate";
    public const string PitRegulation = "PitRegulation";
    public const string MinimumWage = "MinimumWage";
    public const string AllowanceExemptThreshold = "AllowanceExemptThreshold";
    public const string LaborLawConstants = "LaborLawConstants";

    public static readonly IReadOnlyList<string> All =
    [
        SocialInsuranceRate, PitRegulation, MinimumWage, AllowanceExemptThreshold, LaborLawConstants
    ];
}
