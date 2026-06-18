namespace MPS.Application.Features.Compliance.AutoUpdate.Models;

/// <summary>Một "Điều" đã lọc theo scope (lọc 2 tầng nội dung — tầng 2).</summary>
public sealed record ScopedClause(int ArticleNumber, string Text);

/// <summary>Kết quả lọc 2 tầng nội dung trước khi gửi AI.</summary>
public sealed class ScopedContent
{
    public required string SourceText { get; init; }     // toàn văn gốc (để grounding)
    public int TotalClauses { get; init; }
    public List<ScopedClause> Clauses { get; init; } = new();

    /// <summary>Đoạn ghép lại để gửi AI (chỉ điều khoản trong scope).</summary>
    public string ForLlm => string.Join("\n\n", Clauses.Select(c => $"Điều {c.ArticleNumber}. {c.Text}"));
    public bool HasScopedContent => Clauses.Count > 0;
}
