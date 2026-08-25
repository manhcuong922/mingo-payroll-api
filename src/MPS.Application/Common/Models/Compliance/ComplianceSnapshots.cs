using MPS.Domain.Enums.Compliance;

namespace MPS.Application.Common.Models.Compliance;

public sealed record SocialInsuranceFundEntrySnapshot(string FundCode, string FundName, decimal EmployeePercent, decimal EmployerPercent);

#pragma warning disable CS0618 // Obsolete — backward-compat snapshot fields, intentionally kept
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
    decimal EmployeeUnionDuesPercent,
    decimal EmployeeUnionDuesCapPercent,
    decimal ReferenceAmount,
    int SocialCapMultiplier,
    int HealthCapMultiplier,
    int UnemploymentCapMultiplier,
    string LegalReference,
    IReadOnlyList<SocialInsuranceFundEntrySnapshot> Funds,
    string EligibilityMode,
    IReadOnlyList<string> EligibleContractSubtypes,
    int? MinTenureMonths)
{
    public decimal GetFundEmployerPercent(string fundCode) =>
        Funds.FirstOrDefault(f => f.FundCode == fundCode)?.EmployerPercent
        ?? throw new InvalidOperationException($"No social insurance fund with code '{fundCode}'.");

    public decimal GetFundEmployeePercent(string fundCode) =>
        Funds.FirstOrDefault(f => f.FundCode == fundCode)?.EmployeePercent
        ?? throw new InvalidOperationException($"No social insurance fund with code '{fundCode}'.");

    public bool IsEligible(string contractSubtype, int tenureMonths) =>
        EligibilityMode == SocialInsuranceEligibilityModes.ByContractSubtype
            ? EligibleContractSubtypes.Contains(contractSubtype)
            : tenureMonths >= MinTenureMonths;
}
#pragma warning restore CS0618

public sealed record PitBracketSnapshot(int Order, decimal LowerBound, decimal? UpperBound, decimal Rate);

public sealed record PitRegulationSnapshot(
    int VersionId,
    DateOnly ValidFrom,
    decimal PersonalDeductionAmount,
    decimal DependentDeductionAmount,
    int ShortTermContractMaxMonths,
    decimal ShortTermFlatRatePercent,
    decimal ShortTermTaxExemptMonthlyIncome,
    decimal HousingExemptRatePercent,
    IReadOnlyList<PitBracketSnapshot> Brackets,
    string LegalReference)
{
    public string GetApplicableMode(int contractMonths) =>
        contractMonths < ShortTermContractMaxMonths ? "ShortTerm" : "LongTerm";
}


public sealed record MinimumWageRegionSnapshot(string RegionCode, decimal AmountPerMonth);

public sealed record MinimumWageSnapshot(
    int VersionId,
    DateOnly ValidFrom,
    string AppliedRegionCode,
    IReadOnlyList<MinimumWageRegionSnapshot> Regions,
    string LegalReference)
{
    public decimal GetByRegion(string regionCode) =>
        Regions.FirstOrDefault(r => r.RegionCode == regionCode)?.AmountPerMonth
        ?? throw new InvalidOperationException($"No minimum wage for region '{regionCode}'.");

    public decimal GetAppliedAmount() => GetByRegion(AppliedRegionCode);
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
    decimal OvertimeNightExtraPercent,
    decimal NightWorkSurchargePercent,
    decimal OvertimeNormalUpliftPercent,
    TimeOnly NightStartTime,
    TimeOnly NightEndTime,
    decimal StandardWorkingHoursPerDay,
    decimal MaxRegularHoursPerWeek,
    decimal StandardWorkingDaysPerMonth,
    int AnnualLeaveMinDays,
    decimal WorkRateNormalFloor,
    decimal WorkRateWeeklyRestFloor,
    decimal WorkRateHolidayFloor,
    decimal OvertimeMaxHoursPerDay,
    decimal OvertimeMaxHoursPerMonth,
    int OvertimeMaxHoursPerYear,
    string LegalReference,
    bool OvertimeIncomeFullyTaxExempt = false);
