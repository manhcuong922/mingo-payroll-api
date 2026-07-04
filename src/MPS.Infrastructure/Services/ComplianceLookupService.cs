using Microsoft.Extensions.Caching.Memory;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Infrastructure.Services;

public sealed class ComplianceLookupService : IComplianceLookupService
{
    private readonly ISocialInsuranceRateRepository _socialInsurance;
    private readonly IPitRegulationRepository _pit;
    private readonly IMinimumWageRepository _minWage;
    private readonly IAllowanceExemptThresholdRepository _allowanceThreshold;
    private readonly ILaborLawConstantsRepository _laborLaw;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
    private const string CacheNullError = "Cache returned null.";

    public ComplianceLookupService(
        ISocialInsuranceRateRepository socialInsurance,
        IPitRegulationRepository pit,
        IMinimumWageRepository minWage,
        IAllowanceExemptThresholdRepository allowanceThreshold,
        ILaborLawConstantsRepository laborLaw,
        IMemoryCache cache)
    {
        _socialInsurance = socialInsurance;
        _pit = pit;
        _minWage = minWage;
        _allowanceThreshold = allowanceThreshold;
        _laborLaw = laborLaw;
        _cache = cache;
    }

    public async Task<SocialInsuranceRateSnapshot> GetActiveSocialInsuranceAsync(CancellationToken ct)
    {
        const string key = "compliance:SocialInsuranceRateVersion:active";
        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            ConfigureEntry(entry, "SocialInsuranceRateVersion");
            var v = await _socialInsurance.GetActiveAsync(ct)
                ?? throw new InvalidOperationException("No active SocialInsuranceRateVersion found. Run ComplianceActivationJob or seed data.");
            return MapSocialInsurance(v);
        }) ?? throw new InvalidOperationException("Cache returned null for SocialInsuranceRateVersion.");
    }

    public async Task<PitRegulationSnapshot> GetActivePitAsync(CancellationToken ct)
    {
        const string key = "compliance:PitRegulationVersion:active";
        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            ConfigureEntry(entry, "PitRegulationVersion");
            var v = await _pit.GetActiveAsync(ct)
                ?? throw new InvalidOperationException("No active PitRegulationVersion found.");
            return MapPit(v);
        }) ?? throw new InvalidOperationException("Cache returned null for PitRegulationVersion.");
    }

    public async Task<MinimumWageSnapshot> GetActiveMinimumWageAsync(CancellationToken ct)
    {
        const string key = "compliance:MinimumWageVersion:active";
        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            ConfigureEntry(entry, "MinimumWageVersion");
            var v = await _minWage.GetActiveAsync(ct)
                ?? throw new InvalidOperationException("No active MinimumWageVersion found.");
            return MapMinimumWage(v);
        }) ?? throw new InvalidOperationException("Cache returned null for MinimumWageVersion.");
    }

    public async Task<AllowanceExemptThresholdSnapshot?> GetActiveAllowanceThresholdAsync(int allowanceTypeId, CancellationToken ct)
    {
        var key = $"compliance:AllowanceExemptThresholdVersion:active:{allowanceTypeId}";
        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            ConfigureEntry(entry, "AllowanceExemptThresholdVersion");
            var v = await _allowanceThreshold.GetActiveForAllowanceTypeAsync(allowanceTypeId, ct);
            return v is null ? null : MapAllowanceThreshold(v);
        });
    }

    public async Task<LaborLawConstantsSnapshot> GetActiveLaborLawConstantsAsync(CancellationToken ct)
    {
        const string key = "compliance:LaborLawConstantsVersion:active";
        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            ConfigureEntry(entry, "LaborLawConstantsVersion");
            var v = await _laborLaw.GetActiveAsync(ct)
                ?? throw new InvalidOperationException("No active LaborLawConstantsVersion found.");
            return MapLaborLaw(v);
        }) ?? throw new InvalidOperationException("Cache returned null for LaborLawConstantsVersion.");
    }

    public async Task<SocialInsuranceRateSnapshot> GetSocialInsuranceAtAsync(DateOnly payDate, CancellationToken ct)
    {
        var key = $"compliance:SocialInsuranceRateVersion:at:{payDate:yyyyMMdd}";
        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.SlidingExpiration = CacheDuration;
            var v = await _socialInsurance.GetActiveAtAsync(payDate, ct)
                ?? throw new InvalidOperationException($"No SocialInsuranceRateVersion active at {payDate:yyyy-MM-dd}.");
            return MapSocialInsurance(v);
        }) ?? throw new InvalidOperationException(CacheNullError);
    }

    public async Task<PitRegulationSnapshot> GetPitAtAsync(DateOnly payDate, CancellationToken ct)
    {
        var key = $"compliance:PitRegulationVersion:at:{payDate:yyyyMMdd}";
        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.SlidingExpiration = CacheDuration;
            var v = await _pit.GetActiveAtAsync(payDate, ct)
                ?? throw new InvalidOperationException($"No PitRegulationVersion active at {payDate:yyyy-MM-dd}.");
            return MapPit(v);
        }) ?? throw new InvalidOperationException(CacheNullError);
    }

    public async Task<MinimumWageSnapshot> GetMinimumWageAtAsync(DateOnly payDate, CancellationToken ct)
    {
        var key = $"compliance:MinimumWageVersion:at:{payDate:yyyyMMdd}";
        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.SlidingExpiration = CacheDuration;
            var v = await _minWage.GetActiveAtAsync(payDate, ct)
                ?? throw new InvalidOperationException($"No MinimumWageVersion active at {payDate:yyyy-MM-dd}.");
            return MapMinimumWage(v);
        }) ?? throw new InvalidOperationException(CacheNullError);
    }

    public async Task<AllowanceExemptThresholdSnapshot?> GetAllowanceThresholdAtAsync(int allowanceTypeId, DateOnly payDate, CancellationToken ct)
    {
        var key = $"compliance:AllowanceExemptThresholdVersion:at:{payDate:yyyyMMdd}:{allowanceTypeId}";
        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.SlidingExpiration = CacheDuration;
            var v = await _allowanceThreshold.GetActiveAtForAllowanceTypeAsync(allowanceTypeId, payDate, ct);
            return v is null ? null : MapAllowanceThreshold(v);
        });
    }

    public async Task<LaborLawConstantsSnapshot> GetLaborLawConstantsAtAsync(DateOnly payDate, CancellationToken ct)
    {
        var key = $"compliance:LaborLawConstantsVersion:at:{payDate:yyyyMMdd}";
        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.SlidingExpiration = CacheDuration;
            var v = await _laborLaw.GetActiveAtAsync(payDate, ct)
                ?? throw new InvalidOperationException($"No LaborLawConstantsVersion active at {payDate:yyyy-MM-dd}.");
            return MapLaborLaw(v);
        }) ?? throw new InvalidOperationException(CacheNullError);
    }

    private void ConfigureEntry(ICacheEntry entry, string complianceType)
    {
        entry.SlidingExpiration = CacheDuration;
        var token = ComplianceCacheInvalidator.GetToken(complianceType);
        entry.AddExpirationToken(new Microsoft.Extensions.Primitives.CancellationChangeToken(token));
    }

#pragma warning disable CS0618 // Obsolete — backward-compat snapshot fields, intentionally kept
    private static SocialInsuranceRateSnapshot MapSocialInsurance(Domain.Entities.Compliance.SocialInsuranceRateVersion v) =>
        new(v.Id, v.ValidFrom, v.EmployeeSocialPercent, v.EmployerSocialPercent,
            v.EmployerOccupationalDiseasePercent, v.EmployeeHealthPercent, v.EmployerHealthPercent,
            v.EmployeeUnemploymentPercent, v.EmployerUnemploymentPercent, v.EmployerUnionFundPercent,
            v.ReferenceAmount, v.SocialCapMultiplier, v.HealthCapMultiplier, v.UnemploymentCapMultiplier, v.LegalReference,
            v.Funds.Select(f => new SocialInsuranceFundEntrySnapshot(f.FundCode, f.FundName, f.EmployeePercent, f.EmployerPercent)).ToList(),
            v.EligibilityMode,
            string.IsNullOrWhiteSpace(v.EligibleContractSubtypesCsv)
                ? Array.Empty<string>()
                : v.EligibleContractSubtypesCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            v.MinTenureMonths);
#pragma warning restore CS0618

    private static PitRegulationSnapshot MapPit(Domain.Entities.Compliance.PitRegulationVersion v) =>
        new(v.Id, v.ValidFrom, v.PersonalDeductionAmount, v.DependentDeductionAmount,
            v.ShortTermContractMaxMonths, v.ShortTermFlatRatePercent, v.ShortTermTaxExemptMonthlyIncome,
            v.Brackets.OrderBy(b => b.Order).Select(b => new PitBracketSnapshot(b.Order, b.LowerBound, b.UpperBound, b.Rate)).ToList(),
            v.LegalReference);

    private static MinimumWageSnapshot MapMinimumWage(Domain.Entities.Compliance.MinimumWageVersion v) =>
        new(v.Id, v.ValidFrom, v.AppliedRegionCode,
            v.Regions.Select(r => new MinimumWageRegionSnapshot(r.RegionCode, r.AmountPerMonth)).ToList(),
            v.LegalReference);

    private static AllowanceExemptThresholdSnapshot MapAllowanceThreshold(Domain.Entities.Compliance.AllowanceExemptThresholdVersion v) =>
        new(v.Id, v.ValidFrom, v.AllowanceTypeId,
            v.AllowanceType?.Code ?? string.Empty,
            v.AllowanceType?.Name ?? string.Empty,
            v.ThresholdMode, v.MonthlyExemptAmount,
            v.AppliesToTax, v.AppliesToSocial, v.LegalReference);

    private static LaborLawConstantsSnapshot MapLaborLaw(Domain.Entities.Compliance.LaborLawConstantsVersion v) =>
        new(v.Id, v.ValidFrom, v.ProbationSalaryMinPercent, v.OvertimeNightExtraPercent,
            v.NightWorkSurchargePercent, v.OvertimeNormalUpliftPercent,
            v.NightStartTime, v.NightEndTime,
            v.StandardWorkingHoursPerDay, v.MaxRegularHoursPerWeek,
            v.StandardWorkingDaysPerMonth, v.AnnualLeaveMinDays,
            v.WorkRateNormalFloor, v.WorkRateWeeklyRestFloor, v.WorkRateHolidayFloor,
            v.OvertimeMaxHoursPerDay, v.OvertimeMaxHoursPerMonth,
            v.OvertimeMaxHoursPerYear, v.LegalReference);
}
