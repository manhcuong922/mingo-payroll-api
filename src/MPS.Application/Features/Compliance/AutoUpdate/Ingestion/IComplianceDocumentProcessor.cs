using MPS.Application.Features.Compliance.AutoUpdate.Models;

namespace MPS.Application.Features.Compliance.AutoUpdate.Ingestion;

/// <summary>Một văn bản đã có toàn văn, sẵn sàng đưa vào lõi xử lý (không quan tâm nguồn từ đâu).</summary>
/// <param name="ExpectedEntityType">
/// Loại entity mà caller ĐANG rà (xem TargetEntityTypes). Có giá trị thì lõi bắt buộc kết quả AI
/// phán phải trùng — Kênh B đi tìm "ngưỡng phụ cấp" mà vớ phải văn bản khác thì loại luôn, thay vì
/// đẻ ra proposal sai loại entity. Null = không ràng buộc (Kênh A nạp tay, chưa biết trước loại).
/// </param>
/// <param name="EffectiveDate">
/// Ngày hiệu lực caller đã biết. Dùng làm dự phòng khi AI không bóc được ngày trong toàn văn —
/// thiếu nó thì proposal có ProposedValidFrom = null và applier không biết đặt ValidFrom.
/// </param>
public sealed record RawDocument(
    string SourceKey,
    string? Url,
    string? DocumentNo,
    string FullText,
    bool DryRun = false,
    string? ExpectedEntityType = null,
    DateOnly? EffectiveDate = null);

/// <summary>Kết quả xử lý một văn bản qua lõi.</summary>
public enum IngestionKind { Proposal, NoChange, NoScopedContent, Failed }

public sealed class IngestionOutcome
{
    public IngestionKind Kind { get; private init; }
    public int? ProposalId { get; private init; }
    public string? Reason { get; private init; }
    public ValidationReport? Report { get; private init; }
    public DiffResult? Diff { get; private init; }

    public static IngestionOutcome Proposal(int? proposalId, ValidationReport report, DiffResult diff) =>
        new() { Kind = IngestionKind.Proposal, ProposalId = proposalId, Report = report, Diff = diff };

    public static IngestionOutcome NoChange(ValidationReport report, DiffResult diff) =>
        new() { Kind = IngestionKind.NoChange, Report = report, Diff = diff };

    public static IngestionOutcome NoScopedContent(string reason) =>
        new() { Kind = IngestionKind.NoScopedContent, Reason = reason };

    public static IngestionOutcome Failed(string reason) =>
        new() { Kind = IngestionKind.Failed, Reason = reason };
}

/// <summary>
/// LÕI DÙNG CHUNG: nhận toàn văn → scope → extract → validate → diff → (tạo) proposal.
/// Mọi "đường vào văn bản" (crawl, nạp tay, watcher) đều gọi interface này.
/// </summary>
public interface IComplianceDocumentProcessor
{
    Task<IngestionOutcome> ProcessAsync(RawDocument doc, CancellationToken ct);
}
