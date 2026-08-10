using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Validation;

/// <summary>
/// Lớp 3 — so với data đang active + tính diff. "Không khác gì thì không tạo proposal" — yêu cầu cốt lõi.
/// Map cứng targetEntityType → DbSet thật của MPS + flatten đúng schema hiện tại (BHXH dùng Funds).
/// </summary>
public sealed class ComplianceDiffService : IComplianceDiffService
{
    private const string Layer = "Diff";
    private const decimal AbnormalJumpRatio = 0.30m;  // >30% → warning

    private readonly AppDbContext _db;
    private readonly ILogger<ComplianceDiffService> _logger;

    public ComplianceDiffService(AppDbContext db, ILogger<ComplianceDiffService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DiffResult> ComputeDiffAsync(
        ExtractionResult result, ValidationReport report, CancellationToken ct)
    {
        var entityType = result.TargetEntityType ?? "(unknown)";
        var (active, activeValidFrom) = await LoadActiveAsync(result.TargetEntityType, ct);
        var proposed = FlattenProposed(result);

        var diff = new DiffResult { ActiveValidFrom = activeValidFrom };
        var allFields = active.Keys.Union(proposed.Keys).OrderBy(f => f, StringComparer.Ordinal);

        foreach (var field in allFields)
        {
            var hasOld = active.TryGetValue(field, out var oldVal);
            var hasNew = proposed.TryGetValue(field, out var newVal);

            if (hasOld && hasNew)
            {
                if (oldVal == newVal)
                {
                    diff.Entries.Add(new DiffEntry(field, Fmt(oldVal), Fmt(newVal), ChangeType.Unchanged));
                }
                else
                {
                    diff.Entries.Add(new DiffEntry(field, Fmt(oldVal), Fmt(newVal), ChangeType.Changed));
                    WarnIfAbnormal(field, oldVal, newVal, report);
                }
            }
            else if (hasNew)
            {
                diff.Entries.Add(new DiffEntry(field, null, Fmt(newVal), ChangeType.Added));
            }
            else
            {
                // Field đang áp dụng mà lần bóc này không lấy được. KHÔNG phải đề xuất xoá —
                // applier giữ nguyên giá trị active. Đếm riêng để tính ActiveFieldCoverage.
                diff.Entries.Add(new DiffEntry(field, Fmt(oldVal), null, ChangeType.NotExtracted));
            }
        }

        if (!diff.HasSubstantiveChange)
        {
            report.Pass(Layer, entityType, "Data đề xuất trùng data active — không có khác biệt thực chất.");
            _logger.LogInformation("[Validation L3/Diff] entity={Entity}: KHÔNG có khác biệt — sẽ không tạo proposal.",
                result.TargetEntityType);
        }
        else
        {
            report.Pass(Layer, entityType,
                $"Khác biệt: {diff.ChangedCount} changed, {diff.AddedCount} added, " +
                $"{diff.NotExtractedCount} field không bóc được (giữ nguyên giá trị cũ).");
            _logger.LogInformation(
                "[Validation L3/Diff] entity={Entity}: changed={C} added={A} notExtracted={N}.",
                result.TargetEntityType, diff.ChangedCount, diff.AddedCount, diff.NotExtractedCount);
        }

        return diff;
    }

    private void WarnIfAbnormal(string field, decimal oldVal, decimal newVal, ValidationReport report)
    {
        if (oldVal == 0) return;
        var ratio = Math.Abs(newVal - oldVal) / Math.Abs(oldVal);
        if (ratio > AbnormalJumpRatio)
        {
            report.Warn(Layer, field,
                $"Thay đổi bất thường {oldVal:N0} → {newVal:N0} ({ratio:P0}) > 30% — nghi AI đọc sai, cần để mắt.");
        }
    }

    /// <summary>
    /// Flatten data active của đúng entity về map field→decimal (cùng convention với extracted, xem ExtractionPrompt),
    /// kèm ValidFrom của version active để chốt recency dùng lại.
    /// CHỈ đọc data active của MPS — không bao giờ ghi.
    /// </summary>
    private async Task<(Dictionary<string, decimal> Map, DateOnly? ActiveValidFrom)> LoadActiveAsync(
        string? entityType, CancellationToken ct)
    {
        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        DateOnly? validFrom = null;

        switch (entityType)
        {
            case TargetEntityTypes.MinimumWageVersion:
                var mw = await _db.MinimumWageVersions.AsNoTracking().Include(v => v.Regions)
                    .FirstOrDefaultAsync(v => v.IsActive && !v.IsArchived, ct);
                if (mw != null)
                {
                    validFrom = mw.ValidFrom;
                    foreach (var r in mw.Regions) map[$"Region:{r.RegionCode}"] = r.AmountPerMonth;
                }
                break;

            case TargetEntityTypes.SocialInsuranceRateVersion:
                var si = await _db.SocialInsuranceRateVersions.AsNoTracking().Include(v => v.Funds)
                    .FirstOrDefaultAsync(v => v.IsActive && !v.IsArchived, ct);
                if (si != null)
                {
                    validFrom = si.ValidFrom;
                    foreach (var f in si.Funds)
                    {
                        map[$"Fund:{f.FundCode}:Employee"] = f.EmployeePercent;
                        map[$"Fund:{f.FundCode}:Employer"] = f.EmployerPercent;
                    }
                    map["EmployeeHealthPercent"] = si.EmployeeHealthPercent;
                    map["EmployerHealthPercent"] = si.EmployerHealthPercent;
                    map["EmployeeUnemploymentPercent"] = si.EmployeeUnemploymentPercent;
                    map["EmployerUnemploymentPercent"] = si.EmployerUnemploymentPercent;
                    map["EmployerUnionFundPercent"] = si.EmployerUnionFundPercent;
                    map["EmployeeUnionDuesPercent"] = si.EmployeeUnionDuesPercent;
                    map["EmployeeUnionDuesCapPercent"] = si.EmployeeUnionDuesCapPercent;
                    map["ReferenceAmount"] = si.ReferenceAmount;
                }
                break;

            case TargetEntityTypes.PitRegulationVersion:
                var pit = await _db.PitRegulationVersions.AsNoTracking().Include(v => v.Brackets)
                    .FirstOrDefaultAsync(v => v.IsActive && !v.IsArchived, ct);
                if (pit != null)
                {
                    validFrom = pit.ValidFrom;
                    map["PersonalDeductionAmount"] = pit.PersonalDeductionAmount;
                    map["DependentDeductionAmount"] = pit.DependentDeductionAmount;
                    foreach (var br in pit.Brackets) map[$"Bracket:{br.Order}:Rate"] = br.Rate;
                }
                break;

            case TargetEntityTypes.LaborLawConstantsVersion:
                var ll = await _db.LaborLawConstantsVersions.AsNoTracking()
                    .FirstOrDefaultAsync(v => v.IsActive && !v.IsArchived, ct);
                if (ll != null)
                {
                    validFrom = ll.ValidFrom;
                    map["ProbationSalaryMinPercent"] = ll.ProbationSalaryMinPercent;
                    map["OvertimeNightExtraPercent"] = ll.OvertimeNightExtraPercent;
                    map["NightWorkSurchargePercent"] = ll.NightWorkSurchargePercent;
                    map["StandardWorkingHoursPerDay"] = ll.StandardWorkingHoursPerDay;
                    map["MaxRegularHoursPerWeek"] = ll.MaxRegularHoursPerWeek;
                    map["StandardWorkingDaysPerMonth"] = ll.StandardWorkingDaysPerMonth;
                    map["AnnualLeaveMinDays"] = ll.AnnualLeaveMinDays;
                    map["WorkRateNormalFloor"] = ll.WorkRateNormalFloor;
                    map["WorkRateWeeklyRestFloor"] = ll.WorkRateWeeklyRestFloor;
                    map["WorkRateHolidayFloor"] = ll.WorkRateHolidayFloor;
                    map["OvertimeMaxHoursPerDay"] = ll.OvertimeMaxHoursPerDay;
                    map["OvertimeMaxHoursPerMonth"] = ll.OvertimeMaxHoursPerMonth;
                    map["OvertimeMaxHoursPerYear"] = ll.OvertimeMaxHoursPerYear;
                }
                break;

            case TargetEntityTypes.AllowanceExemptThresholdVersion:
                // Có thể có nhiều version active (mỗi AllowanceType một version). Diff theo MonthlyExemptAmount
                // chỉ mang tính tham khảo ở mức tổng — lấy version active đầu tiên có ngưỡng.
                var at = await _db.AllowanceExemptThresholdVersions.AsNoTracking()
                    .FirstOrDefaultAsync(v => v.IsActive && !v.IsArchived && v.MonthlyExemptAmount != null, ct);
                if (at?.MonthlyExemptAmount is { } amt)
                {
                    validFrom = at.ValidFrom;
                    map["MonthlyExemptAmount"] = amt;
                }
                break;
        }
        return (map, validFrom);
    }

    private static Dictionary<string, decimal> FlattenProposed(ExtractionResult r)
    {
        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in r.Values)
            if (v.Number is { } n) map[v.Field] = n;
        return map;
    }

    private static string Fmt(decimal v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
