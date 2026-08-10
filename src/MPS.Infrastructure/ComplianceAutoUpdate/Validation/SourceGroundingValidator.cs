using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Validation;

/// <summary>
/// Lớp 1 — grounding: chống AI bịa số hoặc đọc sai chữ số (3.920.000 ↔ 3.290.000).
///
/// So số phải TÍNH ĐẾN ĐƠN VỊ. Văn bản luật viết "15,5 triệu đồng/tháng" còn hệ thống lưu 15500000;
/// viết cột thuế suất "5" còn hệ thống lưu 0.05. So chuỗi thô kiểu cũ thì toàn bộ 7 field của Luật
/// thuế TNCN đều bị gắn ERROR "nghi AI bịa" dù số hoàn toàn đúng — cảnh báo giả kiểu đó làm người
/// duyệt mất tin vào cả module.
/// </summary>
public sealed class SourceGroundingValidator : ISourceGroundingValidator
{
    private const string Layer = "Grounding";
    private readonly ILogger<SourceGroundingValidator> _logger;

    public SourceGroundingValidator(ILogger<SourceGroundingValidator> logger) => _logger = logger;

    public void Validate(ExtractionResult result, string sourceText, ValidationReport report)
    {
        var sourceDigits = DigitRuns(sourceText);
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

            // 2) con số phải truy được về đúng câu evidence (hoặc ít nhất về văn bản gốc).
            if (v.Number is { } num && !IsGrounded(num, v.Evidence, sourceDigits))
            {
                report.Error(Layer, v.Field,
                    $"Số {num} không truy được về evidence \"{Trunc(v.Evidence)}\" — nghi đọc sai chữ số.");
                continue;
            }

            report.Pass(Layer, v.Field, "Khớp evidence + số liệu với văn bản gốc.");
        }

        _logger.LogInformation(
            "[Validation L1/Grounding] pass={Pass} warn={Warn} error={Err}",
            report.Items.Count(i => i.Layer == Layer && i.Severity == ValidationSeverity.Pass),
            report.Items.Count(i => i.Layer == Layer && i.Severity == ValidationSeverity.Warning),
            report.Items.Count(i => i.Layer == Layer && i.Severity == ValidationSeverity.Error));
    }

    /// <summary>
    /// Ưu tiên đối chiếu với ĐÚNG câu evidence — phạm vi hẹp nên cho phép quy đổi đơn vị mà vẫn khó
    /// trùng ngẫu nhiên. Không thấy thì hạ xuống đối chiếu giá trị NGUYÊN GỐC với toàn văn (đúng
    /// hành vi cũ), phòng khi evidence là câu dẫn không chứa trực tiếp con số.
    /// </summary>
    private static bool IsGrounded(decimal num, string evidence, IReadOnlySet<string> sourceDigits)
    {
        var evidenceDigits = DigitRuns(evidence);
        if (UnitVariants(num).Any(evidenceDigits.Contains)) return true;
        return sourceDigits.Contains(Token(num));
    }

    /// <summary>
    /// Các cách một con số có thể được VIẾT trong văn bản luật, tính cả đơn vị:
    /// 15500000 ↔ "15,5 triệu"; 0.05 ↔ cột thuế suất "5"; 5 ↔ tỷ lệ lưu dạng 0.05.
    /// </summary>
    private static IEnumerable<string> UnitVariants(decimal num)
    {
        yield return Token(num);
        yield return Token(num * 100m);          // tỷ lệ thập phân → phần trăm
        yield return Token(num / 100m);          // phần trăm → tỷ lệ thập phân
        yield return Token(num / 1_000m);        // đồng → nghìn đồng
        yield return Token(num / 1_000_000m);    // đồng → triệu đồng
    }

    /// <summary>
    /// Chuỗi chữ số đại diện cho một giá trị: bỏ dấu thập phân và dấu phân tách, GIỮ số 0 đứng đầu
    /// phần thập phân (0.05 → "005") để không lẫn với 0.5 → "05".
    /// </summary>
    private static string Token(decimal num)
    {
        var s = num.ToString(CultureInfo.InvariantCulture);
        if (s.Contains('.')) s = s.TrimEnd('0').TrimEnd('.');
        return Separators.Replace(s, string.Empty);
    }

    /// <summary>
    /// Các cụm chữ số có trong text, mỗi cụm lấy cả dạng nguyên và dạng bỏ số 0 đứng đầu —
    /// văn bản luật Việt Nam hay viết "08 giờ", "01 ngày".
    /// </summary>
    private static HashSet<string> DigitRuns(string text)
    {
        var runs = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in DigitRun.Matches(Separators.Replace(text, string.Empty)))
        {
            runs.Add(m.Value);
            runs.Add(m.Value.TrimStart('0') is { Length: > 0 } t ? t : "0");
        }
        return runs;
    }

    /// <summary>Dấu phân tách hàng nghìn/thập phân và khoảng trắng — "4.960.000" và "4 960 000" là một.</summary>
    private static readonly Regex Separators = new(@"[.,\s]", RegexOptions.Compiled);

    private static readonly Regex DigitRun = new(@"\d+", RegexOptions.Compiled);

    private static string Trunc(string s) => s.Length <= 80 ? s : s[..80] + "…";
}
