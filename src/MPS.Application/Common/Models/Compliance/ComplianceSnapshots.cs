namespace MPS.Application.Common.Models.Compliance;

public sealed record SocialInsuranceRateSnapshot(
    int VersionId,
    DateOnly ValidFrom,
    decimal EmployeeSocialPercent,
    decimal EmployerSocialPercent,
    decimal EmployerOccupationalDiseasePercent,
    decimal EmployeeHealthPercent,
    decimal EmployerHealthPercent,
    decimal EmployeeUnemploymentPercent,
    decimal EmployerUnemploymentPercent,
    decimal EmployerUnionFundPercent,
    decimal ReferenceAmount,
    int SocialCapMultiplier,
    int UnemploymentCapMultiplier,
    string LegalReference);

public sealed record PitBracketSnapshot(int Order, decimal LowerBound, decimal? UpperBound, decimal Rate);

public sealed record PitRegulationSnapshot(
    int VersionId,
    DateOnly ValidFrom,
    decimal PersonalDeductionAmount,
    decimal DependentDeductionAmount,
    IReadOnlyList<PitBracketSnapshot> Brackets,
    string LegalReference);


public sealed record MinimumWageRegionSnapshot(string RegionCode, decimal AmountPerMonth);

public sealed record MinimumWageSnapshot(
    int VersionId,
    DateOnly ValidFrom,
    IReadOnlyList<MinimumWageRegionSnapshot> Regions,
    string LegalReference)
{
    public decimal GetByRegion(string regionCode) =>
        Regions.FirstOrDefault(r => r.RegionCode == regionCode)?.AmountPerMonth
        ?? throw new InvalidOperationException($"No minimum wage for region '{regionCode}'.");
}

public sealed record AllowanceExemptThresholdSnapshot(
    int VersionId,
    DateOnly ValidFrom,
    int AllowanceTypeId,
    string AllowanceTypeCode,
    string AllowanceTypeName,
    string ThresholdMode,
    decimal? MonthlyExemptAmount,
    bool AppliesToTax,
    bool AppliesToSocial,
    string LegalReference);

public sealed record LaborLawConstantsSnapshot(
    int VersionId,
    DateOnly ValidFrom,
    decimal ProbationSalaryMinPercent,
    decimal NightShiftSurchargePercent,
    decimal StandardWorkingHoursPerDay,
    decimal StandardWorkingDaysPerMonth,
    int AnnualLeaveMinDays,
    decimal OvertimeMaxHoursPerDay,
    int OvertimeMaxHoursPerYear,
    string LegalReference);
