using System.Text;
using System.Text.RegularExpressions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Ai;

/// <summary>
/// Chia đoạn scoped text thành các lượt gọi LLM theo RANH GIỚI ĐIỀU KHOẢN, rồi gộp kết quả lại.
///
/// Lý do tồn tại: trước đây extractor cắt cứng <c>scopedText[..MaxInputChars]</c>. Với văn bản dài
/// (Luật thuế TNCN scoped ~25k ký tự) nhát cắt rơi vào giữa văn bản và nuốt mất đúng điều chứa số
/// liệu (Điều 9 biểu thuế, Điều 10 giảm trừ gia cảnh) — AI trả values rỗng, mọi field active thành
/// NotExtracted, proposal vô nghĩa. Cắt theo "Điều N." thì không điều nào bị mất nửa chừng.
/// </summary>
internal static class ExtractionBatcher
{
    /// <summary>Cùng mốc nhận dạng điều khoản với <c>ContentScopeFilter</c>.</summary>
    private static readonly Regex ArticleSplit =
        new(@"(?=Điều\s+\d+\s*\.)", RegexOptions.Compiled);

    /// <summary>
    /// Gom các điều thành lô ≤ <paramref name="maxChars"/>. Một điều dài hơn cả lô thì đành cắt cứng
    /// riêng nó (hiếm, và mất mát khoanh trong đúng điều đó thay vì cả phần đuôi văn bản).
    /// Trả tối đa <paramref name="maxBatches"/> lô để chặn chi phí với văn bản khổng lồ.
    /// </summary>
    public static IReadOnlyList<string> Split(string scopedText, int maxChars, int maxBatches)
    {
        if (string.IsNullOrWhiteSpace(scopedText)) return Array.Empty<string>();
        if (maxChars <= 0 || scopedText.Length <= maxChars) return new[] { scopedText };

        var batches = new List<string>();
        var current = new StringBuilder();

        foreach (var clause in SplitClauses(scopedText, maxChars))
        {
            if (current.Length > 0 && current.Length + clause.Length > maxChars)
            {
                batches.Add(current.ToString().Trim());
                current.Clear();
                if (batches.Count >= maxBatches) return batches;
            }

            if (current.Length > 0) current.Append("\n\n");
            current.Append(clause);
        }

        if (current.Length > 0 && batches.Count < maxBatches)
            batches.Add(current.ToString().Trim());

        return batches;
    }

    /// <summary>Tách theo "Điều N."; điều nào dài quá một lô thì bổ nhỏ tiếp bằng cắt cứng.</summary>
    private static IEnumerable<string> SplitClauses(string text, int maxChars)
    {
        foreach (var raw in ArticleSplit.Split(text))
        {
            var clause = raw.Trim();
            if (clause.Length == 0) continue;

            if (clause.Length <= maxChars)
            {
                yield return clause;
                continue;
            }

            for (var i = 0; i < clause.Length; i += maxChars)
                yield return clause.Substring(i, Math.Min(maxChars, clause.Length - i));
        }
    }

    /// <summary>
    /// Gộp kết quả nhiều lô thành một. Field trùng tên: lô nào bóc được SỐ trước thì thắng
    /// (lô sau chỉ điền vào chỗ trống, không ghi đè). <c>targetEntityType</c> lấy theo đa số —
    /// một lô lạc đề ("phụ cấp" trong luật thuế) không kéo được cả văn bản sang entity khác.
    /// </summary>
    public static ExtractionResult Merge(IReadOnlyList<ExtractionResult> parts)
    {
        if (parts.Count == 1) return parts[0];

        var merged = new ExtractionResult
        {
            DocumentNo = parts.Select(p => p.DocumentNo).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)),
            EffectiveDate = parts.Select(p => p.EffectiveDate).FirstOrDefault(d => d is not null),
            TargetEntityType = MajorityEntityType(parts),
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in parts.SelectMany(p => p.Values))
        {
            if (string.IsNullOrWhiteSpace(value.Field)) continue;
            if (value.Number is null && value.Text is null) continue;
            if (!seen.Add(value.Field)) continue;
            merged.Values.Add(value);
        }

        return merged;
    }

    private static string? MajorityEntityType(IReadOnlyList<ExtractionResult> parts) =>
        parts
            .Select(p => p.TargetEntityType)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .GroupBy(t => t!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
}
