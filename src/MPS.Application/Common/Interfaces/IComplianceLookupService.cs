using MPS.Application.Common.Models.Compliance;

namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Read-side lookup cho Payroll Engine. Chỉ publish rates/amounts theo luật.
/// Eligibility (thử việc có đóng BHXH không, nhân viên ở vùng nào) là engine concern.
/// payDate trong GetXxxAt() không bị validate &gt; today — engine có thể preview kỳ tương lai.
/// </summary>
public interface IComplianceLookupService
{
    Task<SocialInsuranceRateSnapshot> GetActiveSocialInsuranceAsync(CancellationToken ct);
    Task<PitRegulationSnapshot> GetActivePitAsync(CancellationToken ct);
    Task<OtRateSnapshot> GetActiveOtRatesAsync(CancellationToken ct);
    Task<MinimumWageSnapshot> GetActiveMinimumWageAsync(CancellationToken ct);
    Task<AllowanceExemptThresholdSnapshot?> GetActiveAllowanceThresholdAsync(int allowanceTypeId, CancellationToken ct);
    Task<LaborLawConstantsSnapshot> GetActiveLaborLawConstantsAsync(CancellationToken ct);

    Task<SocialInsuranceRateSnapshot> GetSocialInsuranceAtAsync(DateOnly payDate, CancellationToken ct);
    Task<PitRegulationSnapshot> GetPitAtAsync(DateOnly payDate, CancellationToken ct);
    Task<OtRateSnapshot> GetOtRatesAtAsync(DateOnly payDate, CancellationToken ct);
    Task<MinimumWageSnapshot> GetMinimumWageAtAsync(DateOnly payDate, CancellationToken ct);
    Task<AllowanceExemptThresholdSnapshot?> GetAllowanceThresholdAtAsync(int allowanceTypeId, DateOnly payDate, CancellationToken ct);
    Task<LaborLawConstantsSnapshot> GetLaborLawConstantsAtAsync(DateOnly payDate, CancellationToken ct);
}
