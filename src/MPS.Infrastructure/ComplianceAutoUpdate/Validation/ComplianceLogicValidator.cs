using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Domain.Enums.Compliance;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Validation;

/// <summary>Lớp 2 — kiểm tra logic dùng hằng số trong enum classes của hệ thống làm chuẩn.</summary>
public sealed class ComplianceLogicValidator : IComplianceLogicValidator
{
    private const string Layer = "Logic";
    private readonly ILogger<ComplianceLogicValidator> _logger;

    public ComplianceLogicValidator(ILogger<ComplianceLogicValidator> logger) => _logger = logger;

    public void Validate(ExtractionResult result, ValidationReport report)
    {
        switch (result.TargetEntityType)
        {
            case TargetEntityTypes.MinimumWageVersion: ValidateMinimumWage(result, report); break;
            case TargetEntityTypes.PitRegulationVersion: ValidatePit(result, report); break;
            case TargetEntityTypes.SocialInsuranceRateVersion: ValidateSocial(result, report); break;
            case TargetEntityTypes.AllowanceExemptThresholdVersion: ValidateThreshold(result, report); break;
            case TargetEntityTypes.LaborLawConstantsVersion:
                report.Pass(Layer, "LaborLawConstants", "Không có rule logic cứng — bỏ qua.");
                break;
            default:
                report.Warn(Layer, "targetEntityType",
                    $"targetEntityType không xác định: '{result.TargetEntityType}'.");
                break;
        }

        _logger.LogInformation(
            "[Validation L2/Logic] entity={Entity} pass={Pass} warn={Warn} error={Err}",
            result.TargetEntityType,
            report.Items.Count(i => i.Layer == Layer && i.Severity == ValidationSeverity.Pass),
            report.Items.Count(i => i.Layer == Layer && i.Severity == ValidationSeverity.Warning),
            report.Items.Count(i => i.Layer == Layer && i.Severity == ValidationSeverity.Error));
    }

    private static void ValidateMinimumWage(ExtractionResult r, ValidationReport report)
    {
        var byRegion = new Dictionary<string, decimal>();
        foreach (var v in r.Values)
        {
            if (!v.Field.StartsWith("Region:", StringComparison.OrdinalIgnoreCase)) continue;
            var code = v.Field["Region:".Length..].Trim().ToUpperInvariant();
            if (v.Number is { } amt) byRegion[code] = amt;
        }

        // đủ 4 vùng theo RegionCodes.All
        foreach (var code in RegionCodes.All)
        {
            if (!byRegion.ContainsKey(code))
                report.Error(Layer, $"Region:{code}", "Thiếu vùng trong RegionCodes.All.");
        }

        // mỗi mức trong khoảng hợp lý 3–10 triệu
        foreach (var (code, amt) in byRegion)
        {
            if (amt < 3_000_000m || amt > 10_000_000m)
                report.Warn(Layer, $"Region:{code}", $"Mức {amt:N0} ngoài khoảng hợp lý 3–10 triệu.");
        }

        // vùng I > II > III > IV
        for (var i = 0; i < RegionCodes.All.Count - 1; i++)
        {
            var hi = RegionCodes.All[i];
            var lo = RegionCodes.All[i + 1];
            if (byRegion.TryGetValue(hi, out var a) && byRegion.TryGetValue(lo, out var b) && a <= b)
                report.Error(Layer, $"Region:{hi}>{lo}", $"Vi phạm thứ tự vùng: {hi}({a:N0}) phải > {lo}({b:N0}).");
        }

        if (!report.Items.Any(i => i.Layer == Layer && i.Severity == ValidationSeverity.Error))
            report.Pass(Layer, "MinimumWage", "4 vùng hợp lệ, đúng thứ tự, trong khoảng.");
    }

    private static void ValidatePit(ExtractionResult r, ValidationReport report)
    {
        // gom theo Bracket:{order}:Rate
        var orders = r.Values
            .Where(v => v.Field.StartsWith("Bracket:", StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Field.Split(':'))
            .Where(p => p.Length == 3 && int.TryParse(p[1], out _))
            .Select(p => int.Parse(p[1]))
            .Distinct().OrderBy(x => x).ToList();

        if (orders.Count == 0)
        {
            report.Warn(Layer, "PitBrackets", "Không bóc được bậc thuế nào.");
            return;
        }

        // Order liên tục
        for (var i = 0; i < orders.Count; i++)
            if (orders[i] != i + 1)
                report.Error(Layer, "PitBrackets", $"Order không liên tục tại {orders[i]} (kỳ vọng {i + 1}).");

        // Rate tăng dần
        decimal? prevRate = null;
        foreach (var o in orders)
        {
            var rate = r.Values.FirstOrDefault(v =>
                v.Field.Equals($"Bracket:{o}:Rate", StringComparison.OrdinalIgnoreCase))?.Number;
            if (rate is { } cur)
            {
                if (prevRate is { } pr && cur <= pr)
                    report.Error(Layer, $"Bracket:{o}:Rate", $"Thuế suất không tăng dần ({pr} → {cur}).");
                prevRate = cur;
            }
        }
        report.Pass(Layer, "PitBrackets", $"Đã kiểm tra {orders.Count} bậc.");
    }

    private static void ValidateSocial(ExtractionResult r, ValidationReport report)
    {
        foreach (var v in r.Values.Where(v => v.Number is not null))
        {
            var p = v.Number!.Value;
            // ReferenceAmount là số tiền (triệu), không phải tỷ lệ → bỏ qua kiểm biên %.
            if (v.Field.Equals("ReferenceAmount", StringComparison.OrdinalIgnoreCase)) continue;
            if (p < 0) report.Error(Layer, v.Field, $"Tỷ lệ âm: {p}.");
            else if (p > 35) report.Warn(Layer, v.Field, $"Tỷ lệ {p}% cao bất thường — cần để mắt.");
        }
        report.Pass(Layer, "Social", "Đã kiểm tra biên các tỷ lệ.");
    }

    private static void ValidateThreshold(ExtractionResult r, ValidationReport report)
    {
        var mode = r.Values.FirstOrDefault(v =>
            v.Field.Equals("ThresholdMode", StringComparison.OrdinalIgnoreCase))?.Text;
        if (mode != null && !ThresholdModes.All.Contains(mode))
            report.Error(Layer, "ThresholdMode", $"ThresholdMode '{mode}' không nằm trong ThresholdModes.All.");
        else
            report.Pass(Layer, "ThresholdMode", "ThresholdMode hợp lệ (hoặc không có).");
    }
}
