using MPS.Application.Features.Compliance.AutoUpdate.Models;

namespace MPS.Application.Features.Compliance.AutoUpdate.Abstractions;

/// <summary>Adapter crawl một nguồn pháp luật. Mỗi nguồn một implementation.</summary>
public interface ILegalSourceAdapter
{
    string SourceKey { get; }
    bool Enabled { get; }

    /// <summary>Tải danh sách "văn bản mới" (đã lọc lĩnh vực ở tầng URL).</summary>
    Task<IReadOnlyList<LegalDocumentRef>> FetchNewListAsync(CancellationToken ct);

    /// <summary>Tải toàn văn một văn bản. Trả rỗng nếu trang render bằng JS (caller mark Failed).</summary>
    Task<string> FetchFullTextAsync(string url, CancellationToken ct);

    /// <summary>HTML thô của trang danh sách (cho fingerprint Cấp 1).</summary>
    Task<string> FetchListHtmlAsync(CancellationToken ct);
}

/// <summary>Cấp 1 — fingerprint cả danh sách.</summary>
public interface IListFingerprintDetector
{
    /// <summary>Trích phần ổn định (tiêu đề + số hiệu), bỏ phần biến động.</summary>
    string ExtractStableContent(string listHtml);

    /// <summary>true nếu danh sách thay đổi so với snapshot (và đã cập nhật snapshot).</summary>
    Task<bool> HasChangedAsync(string sourceKey, string listHtml, CancellationToken ct);
}

/// <summary>Cấp 2 — lọc văn bản đã xử lý.</summary>
public interface ICrawledDocumentTracker
{
    Task<bool> IsProcessedAsync(string url, CancellationToken ct);
    Task MarkDetectedAsync(LegalDocumentRef doc, CancellationToken ct);
    Task UpdateStatusAsync(string url, string status, string? failureReason, CancellationToken ct);
}

/// <summary>Lọc 2 tầng nội dung trước khi gửi AI.</summary>
public interface IContentScopeFilter
{
    ScopedContent Filter(string fullText);
}

/// <summary>AI extract số liệu.</summary>
public interface IComplianceExtractor
{
    Task<ExtractionResult?> ExtractAsync(string scopedText, CancellationToken ct);
}

/// <summary>Lớp 1 — grounding/đối chiếu nguồn.</summary>
public interface ISourceGroundingValidator
{
    void Validate(ExtractionResult result, string sourceText, ValidationReport report);
}

/// <summary>Lớp 2 — kiểm tra logic.</summary>
public interface IComplianceLogicValidator
{
    void Validate(ExtractionResult result, ValidationReport report);
}

/// <summary>Lớp 3 — so với data active + tính diff.</summary>
public interface IComplianceDiffService
{
    /// <summary>Load version active tương ứng, so field-by-field, ghi warning bất thường vào report.</summary>
    Task<DiffResult> ComputeDiffAsync(ExtractionResult result, ValidationReport report, CancellationToken ct);
}

/// <summary>Pipeline ghép toàn bộ luồng. Trả tóm tắt một lần chạy.</summary>
public interface IComplianceCrawlPipeline
{
    Task<ComplianceCrawlSummary> ExecuteAsync(Guid runId, CancellationToken ct);
}
