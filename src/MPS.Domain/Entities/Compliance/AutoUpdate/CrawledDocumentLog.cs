namespace MPS.Domain.Entities.Compliance.AutoUpdate;

/// <summary>Trạng thái xử lý một văn bản đã crawl.</summary>
public static class ExtractStatuses
{
    public const string Detected = "Detected";
    public const string Extracted = "Extracted";
    public const string ProposalCreated = "ProposalCreated";
    public const string NoChange = "NoChange";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}

/// <summary>Cấp 2 — log văn bản đã xử lý, chống xử lý trùng dựa trên <see cref="UrlHash"/>.</summary>
public sealed class CrawledDocumentLog
{
    public int Id { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    public string DocumentUrl { get; set; } = string.Empty;

    /// <summary>SHA256 hex của URL — UNIQUE, dùng để check trùng.</summary>
    public string UrlHash { get; set; } = string.Empty;

    public string? DocumentNo { get; set; }
    public string? Title { get; set; }

    /// <summary>Xem <see cref="ExtractStatuses"/>.</summary>
    public string ExtractStatus { get; set; } = ExtractStatuses.Detected;
    public string? FailureReason { get; set; }

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
