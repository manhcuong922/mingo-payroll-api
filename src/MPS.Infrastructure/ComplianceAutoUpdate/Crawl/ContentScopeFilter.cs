using System.Text.RegularExpressions;
using MPS.Application.Features.Compliance.AutoUpdate.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Crawl;

/// <summary>
/// Lọc 2 tầng nội dung — tầng 2: chunk theo "Điều N." rồi giữ điều có keyword scope.
/// </summary>
public sealed class ContentScopeFilter : IContentScopeFilter
{
    // Tách theo "Điều \d+." — đơn vị tự nhiên của luật VN.
    private static readonly Regex ArticleSplit =
        new(@"(?=Điều\s+\d+\s*\.)", RegexOptions.Compiled);

    private static readonly Regex ArticleHead =
        new(@"^Điều\s+(\d+)\s*\.", RegexOptions.Compiled);

    // Keyword scope đúng phạm vi compliance của hệ thống.
    private static readonly string[] ScopeKeywords =
    {
        "bảo hiểm xã hội", "bảo hiểm y tế", "bảo hiểm thất nghiệp", "kinh phí công đoàn",
        "lương tối thiểu", "mức lương tối thiểu", "vùng i", "vùng ii", "vùng iii", "vùng iv",
        "thuế thu nhập cá nhân", "biểu thuế", "bậc", "thuế suất", "giảm trừ gia cảnh",
        "làm thêm giờ", "tăng ca", "phụ cấp", "mức tham chiếu",
    };

    public ScopedContent Filter(string fullText)
    {
        if (string.IsNullOrWhiteSpace(fullText))
            return new ScopedContent { SourceText = fullText ?? string.Empty, TotalClauses = 0 };

        var chunks = ArticleSplit.Split(fullText)
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .ToList();

        var scoped = new List<ScopedClause>();
        var total = 0;
        foreach (var chunk in chunks)
        {
            var head = ArticleHead.Match(chunk);
            if (!head.Success) continue;   // phần mở đầu trước "Điều 1" — bỏ
            total++;

            var lower = chunk.ToLowerInvariant();
            if (!ScopeKeywords.Any(k => lower.Contains(k))) continue;

            var num = int.Parse(head.Groups[1].Value);
            var body = chunk[head.Length..].Trim();
            scoped.Add(new ScopedClause(num, body));
        }

        return new ScopedContent
        {
            SourceText = fullText,
            TotalClauses = total,
            Clauses = scoped,
        };
    }
}
