using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Domain.Enums.Compliance;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Ai;

/// <summary>
/// Extractor offline (regex), KHÔNG gọi LLM — dùng khi thiếu API key để chạy/test toàn luồng.
/// Bóc số liệu phổ biến nhất (lương tối thiểu vùng) cộng evidence là câu chứa con số.
/// Đủ để minh hoạ grounding/logic/diff; production nên dùng <see cref="AnthropicComplianceExtractor"/>.
/// </summary>
public sealed class OfflineComplianceExtractor : IComplianceExtractor
{
    private readonly ILogger<OfflineComplianceExtractor> _logger;

    // "vùng I ... 4.960.000 đồng" — bắt mã vùng + số tiền gần nhất.
    private static readonly Regex RegionAmount = new(
        @"v[ùu]ng\s+(I{1,3}V?|IV)\b[^\d]{0,40}?([\d\.\,]{7,})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DocNo = new(@"\d{1,4}/\d{4}/[A-ZĐ\-]+", RegexOptions.Compiled);
    private static readonly Regex EffDate = new(
        @"hi[ệe]u\s+l[ự]c[^\d]{0,30}?(\d{1,2})[/\-](\d{1,2})[/\-](\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public OfflineComplianceExtractor(ILogger<OfflineComplianceExtractor> logger)
    {
        _logger = logger;
        _logger.LogWarning(
            "Dùng OfflineComplianceExtractor (regex, không LLM) — thiếu API key. Chỉ phù hợp test.");
    }

    public Task<ExtractionResult?> ExtractAsync(string scopedText, CancellationToken ct)
    {
        var result = new ExtractionResult();

        var docNo = DocNo.Match(scopedText);
        if (docNo.Success) result.DocumentNo = docNo.Value;

        var eff = EffDate.Match(scopedText);
        if (eff.Success &&
            int.TryParse(eff.Groups[1].Value, out var d) &&
            int.TryParse(eff.Groups[2].Value, out var mo) &&
            int.TryParse(eff.Groups[3].Value, out var y))
        {
            try { result.EffectiveDate = new DateOnly(y, mo, d); } catch { /* ignore */ }
        }

        foreach (Match m in RegionAmount.Matches(scopedText))
        {
            var region = NormalizeRegion(m.Groups[1].Value);
            if (region == null) continue;
            var amount = ParseVnNumber(m.Groups[2].Value);
            if (amount is null) continue;

            result.Values.Add(new ExtractedValue
            {
                Field = $"Region:{region}",
                Number = amount,
                Evidence = SentenceAround(scopedText, m.Index),
            });
        }

        if (result.Values.Count > 0)
            result.TargetEntityType = TargetEntityTypes.MinimumWageVersion;

        result.RawJson = JsonSerializer.Serialize(new
        {
            documentNo = result.DocumentNo,
            effectiveDate = result.EffectiveDate?.ToString("yyyy-MM-dd"),
            targetEntityType = result.TargetEntityType,
            values = result.Values,
        }, new JsonSerializerOptions { WriteIndented = true });

        return Task.FromResult<ExtractionResult?>(result);
    }

    private static string? NormalizeRegion(string raw)
    {
        var r = raw.ToUpperInvariant();
        return RegionCodes.All.Contains(r) ? r : null;
    }

    private static decimal? ParseVnNumber(string raw)
    {
        var digits = Regex.Replace(raw, @"[^\d]", "");
        return decimal.TryParse(digits, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static string SentenceAround(string text, int index)
    {
        var start = text.LastIndexOfAny(new[] { '.', '\n', ';' }, Math.Min(index, text.Length - 1));
        var end = text.IndexOfAny(new[] { '.', '\n', ';' }, Math.Min(index, text.Length - 1));
        start = start < 0 ? 0 : start + 1;
        end = end < 0 ? text.Length : end;
        return text[start..end].Trim();
    }
}
