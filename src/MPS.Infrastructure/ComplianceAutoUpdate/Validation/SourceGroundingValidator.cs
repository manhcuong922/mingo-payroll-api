using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Validation;

/// <summary>
/// Lớp 1 — grounding: chống AI bịa số hoặc đọc sai chữ số (3.920.000 ↔ 3.290.000).
/// </summary>
public sealed class SourceGroundingValidator : ISourceGroundingValidator
{
    private const string Layer = "Grounding";
    private readonly ILogger<SourceGroundingValidator> _logger;

    public SourceGroundingValidator(ILogger<SourceGroundingValidator> logger) => _logger = logger;

    public void Validate(ExtractionResult result, string sourceText, ValidationReport report)
    {
        var normSource = NormalizeNumbers(sourceText);
        var lowerSource = sourceText.ToLowerInvariant();

        foreach (var v in result.Values)
        {
            // 1) evidence phải thực sự xuất hiện trong text gốc.
            if (string.IsNullOrWhiteSpace(v.Evidence))
            {
                report.Error(Layer, v.Field, "Thiếu evidence — nghi AI bịa.");
                continue;
            }
            if (!lowerSource.Contains(v.Evidence.ToLowerInvariant().Trim()))
            {
                report.Error(Layer, v.Field,
                    $"Evidence không xuất hiện trong văn bản gốc — nghi AI bịa: \"{Trunc(v.Evidence)}\".");
                continue;
            }

            // 2) con số (đã normalize) phải có trong text gốc đã normalize.
            if (v.Number is { } num)
            {
                var normNum = NormalizeNumberToken(num);
                if (!normSource.Contains(normNum))
                {
                    report.Error(Layer, v.Field,
                        $"Số {num} không tìm thấy trong văn bản (đã normalize) — nghi đọc sai chữ số.");
                    continue;
                }
            }

            report.Pass(Layer, v.Field, "Khớp evidence + số liệu với văn bản gốc.");
        }

        _logger.LogInformation(
            "[Validation L1/Grounding] pass={Pass} warn={Warn} error={Err}",
            report.Items.Count(i => i.Layer == Layer && i.Severity == ValidationSeverity.Pass),
            report.Items.Count(i => i.Layer == Layer && i.Severity == ValidationSeverity.Warning),
            report.Items.Count(i => i.Layer == Layer && i.Severity == ValidationSeverity.Error));
    }

    /// <summary>Bỏ dấu phân tách . , space để so khớp con số.</summary>
    private static string NormalizeNumbers(string s) => Regex.Replace(s, @"[.\,\s]", "");

    private static string NormalizeNumberToken(decimal num)
    {
        var s = num.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Regex.Replace(s, @"[.\,\s]", "");
    }

    private static string Trunc(string s) => s.Length <= 80 ? s : s[..80] + "…";
}
