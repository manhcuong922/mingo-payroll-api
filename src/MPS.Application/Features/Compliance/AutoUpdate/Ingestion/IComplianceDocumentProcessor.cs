using MPS.Application.Features.Compliance.AutoUpdate.Models;

namespace MPS.Application.Features.Compliance.AutoUpdate.Ingestion;

/// <summary>Một văn bản đã có toàn văn, sẵn sàng đưa vào lõi xử lý (không quan tâm nguồn từ đâu).</summary>
public sealed record RawDocument(
    string SourceKey,
    string? Url,
    string? DocumentNo,
    string FullText,
    bool DryRun = false);

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
