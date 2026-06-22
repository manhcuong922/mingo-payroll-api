namespace MPS.Domain.Entities.Compliance.AutoUpdate;

/// <summary>Trạng thái một đầu mối (Lead) do watcher chủ động (Kênh B) sinh ra.</summary>
public static class LeadStatuses
{
    /// <summary>URL nguồn đã grounding → tạo proposal chờ duyệt.</summary>
    public const string Proposal = "Proposal";

    /// <summary>URL nguồn đã grounding nhưng số liệu trùng data active — không tạo proposal.</summary>
    public const string NoChange = "NoChange";

    /// <summary>Không tự xác minh được (web search không khả dụng / thiếu URL / fetch rỗng) — cần người kiểm tra.</summary>
    public const string NeedsHuman = "NeedsHuman";

    /// <summary>Đã được con người xử lý/bỏ qua.</summary>
    public const string Dismissed = "Dismissed";
}

/// <summary>
/// Đầu mối "data có thể đã lỗi thời" do watcher chủ động (LLM + web search) phát hiện.
/// LLM chỉ "chỉ điểm" (URL/claim); con số vào proposal vẫn bóc lại từ văn bản thật qua lõi.
/// </summary>
public sealed class ComplianceLead
{
    public int Id { get; set; }

    /// <summary>Xem <see cref="TargetEntityTypes"/>.</summary>
    public string TargetEntityType { get; set; } = string.Empty;

    /// <summary>Nguồn sinh Lead, vd "watcher-websearch".</summary>
    public string SourceKey { get; set; } = string.Empty;

    // Claim từ LLM (chưa kiểm chứng) — để người duyệt tra khi cần.
    public string? ClaimedDocumentNo { get; set; }
    public DateOnly? ClaimedEffectiveDate { get; set; }
    public string? ClaimedSourceUrl { get; set; }
    public string? ClaimSummary { get; set; }

    /// <summary>Xem <see cref="LeadStatuses"/>.</summary>
    public string Status { get; set; } = LeadStatuses.NeedsHuman;

    /// <summary>Proposal sinh ra (nếu Status = Proposal).</summary>
    public int? ProposalId { get; set; }
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
