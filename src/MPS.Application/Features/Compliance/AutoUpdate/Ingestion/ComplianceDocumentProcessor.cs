using System.Text.Json;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Application.Options;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.AutoUpdate.Ingestion;

/// <summary>
/// LÕI DÙNG CHUNG cho mọi đường vào văn bản (crawl, nạp tay, sau này là watcher).
/// Nhận TOÀN VĂN đã có sẵn → lọc scope → AI extract → 3 lớp validation → diff với data active
/// → tạo proposal (PendingReview) nếu có khác biệt.
///
/// KHÔNG biết gì về crawl/tracker/HTTP — phần discovery do caller lo. Tách ra từ
/// ComplianceCrawlPipeline.ProcessDocumentAsync để Kênh A (upload/URL) tái dùng nguyên xi.
/// </summary>
public sealed class ComplianceDocumentProcessor : IComplianceDocumentProcessor
{
    private readonly IContentScopeFilter _scopeFilter;
    private readonly IComplianceExtractor _extractor;
    private readonly ISourceGroundingValidator _grounding;
    private readonly IComplianceLogicValidator _logic;
    private readonly IComplianceDiffService _diff;
    private readonly IComplianceUpdateProposalRepository _proposals;
    private readonly ComplianceAutoUpdateOptions _options;
    private readonly ILogger<ComplianceDocumentProcessor> _logger;

    /// <summary>Trùng "Layer" của ComplianceDiffService — cùng một lớp validation.</summary>
    private const string DiffLayer = "Diff";

    public ComplianceDocumentProcessor(
        IContentScopeFilter scopeFilter,
        IComplianceExtractor extractor,
        ISourceGroundingValidator grounding,
        IComplianceLogicValidator logic,
        IComplianceDiffService diff,
        IComplianceUpdateProposalRepository proposals,
        ComplianceAutoUpdateOptions options,
        ILogger<ComplianceDocumentProcessor> logger)
    {
        _scopeFilter = scopeFilter;
        _extractor = extractor;
        _grounding = grounding;
        _logic = logic;
        _diff = diff;
        _proposals = proposals;
        _options = options;
        _logger = logger;
    }

    public async Task<IngestionOutcome> ProcessAsync(RawDocument doc, CancellationToken ct)
    {
        var label = doc.Url ?? doc.DocumentNo ?? doc.SourceKey;

        if (string.IsNullOrWhiteSpace(doc.FullText))
            return IngestionOutcome.Failed("Toàn văn rỗng — không có nội dung để xử lý.");

        // ---- Lọc 2 tầng nội dung ----
        var scoped = _scopeFilter.Filter(doc.FullText);
        _logger.LogInformation(
            "[Processor] {Label}: lọc 2 tầng — {Total} điều → {Kept} điều trong scope.",
            label, scoped.TotalClauses, scoped.Clauses.Count);

        if (!scoped.HasScopedContent)
            return IngestionOutcome.NoScopedContent("Không có điều khoản trong scope.");

        // ---- AI extract ----
        var result = await _extractor.ExtractAsync(scoped.ForLlm, ct);
        if (result == null)
            return IngestionOutcome.Failed("AI extract/parse lỗi.");

        result.DocumentNo ??= doc.DocumentNo;
        result.EffectiveDate ??= doc.EffectiveDate;
        _logger.LogInformation("[Processor] {Label}: AI bóc {Count} field, entity={Entity}.",
            label, result.Values.Count, result.TargetEntityType ?? "(?)");

        // ---- Chốt 1: văn bản phải đúng loại entity caller đang rà ----
        if (!string.IsNullOrWhiteSpace(doc.ExpectedEntityType)
            && !string.IsNullOrWhiteSpace(result.TargetEntityType)
            && !string.Equals(result.TargetEntityType, doc.ExpectedEntityType, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "[Processor] {Label}: đang rà {Expected} nhưng AI phán văn bản thuộc {Actual} — bỏ, không tạo proposal.",
                label, doc.ExpectedEntityType, result.TargetEntityType);
            return IngestionOutcome.Failed(
                $"Văn bản thuộc loại {result.TargetEntityType}, không phải {doc.ExpectedEntityType} đang rà soát.");
        }

        // ---- 3 lớp validation (tuần tự, độc lập) ----
        var report = new ValidationReport();
        _grounding.Validate(result, scoped.SourceText, report);
        _logic.Validate(result, report);
        var diff = await _diff.ComputeDiffAsync(result, report, ct);

        // ---- Không khác biệt → không tạo proposal ----
        if (!diff.HasSubstantiveChange)
        {
            _logger.LogInformation("[Processor] {Label}: data đã trùng — NoChange, không tạo proposal.", label);
            return IngestionOutcome.NoChange(report, diff);
        }

        // ---- Chốt 2: bóc hụt quá nhiều field đang áp dụng → coi là extract hỏng ----
        // Mỗi field hụt thành một entry Removed, mà Removed vẫn tính là "thay đổi thực chất".
        // Không chặn ở đây thì proposal sinh ra toàn field bị xoá, duyệt nhầm là mất sạch dữ liệu đúng.
        var minCoverage = _options.MinActiveFieldCoverage;
        if (minCoverage > 0 && diff.ActiveFieldCount > 0 && diff.ActiveFieldCoverage < minCoverage)
        {
            var message =
                $"Chỉ bóc lại được {diff.CoveredFieldCount}/{diff.ActiveFieldCount} field đang áp dụng " +
                $"({diff.ActiveFieldCoverage:P0} < {minCoverage:P0}) — nghi AI bóc hụt chứ không phải văn bản bãi bỏ.";
            report.Error(DiffLayer, result.TargetEntityType ?? "(unknown)", message);
            _logger.LogWarning("[Processor] {Label}: {Message} Không tạo proposal.", label, message);
            return IngestionOutcome.Failed(message);
        }

        // ---- Tạo proposal (PendingReview) ----
        var proposal = new ComplianceUpdateProposal
        {
            SourceKey = doc.SourceKey,
            DocumentUrl = doc.Url ?? string.Empty,
            DocumentNo = result.DocumentNo,
            TargetEntityType = result.TargetEntityType ?? "(unknown)",
            ProposedValidFrom = result.EffectiveDate,
            ExtractedJson = result.RawJson,
            DiffJson = JsonSerializer.Serialize(diff.Entries),
            ValidationReport = JsonSerializer.Serialize(report.Items),
            Status = ProposalStatuses.PendingReview,
            CreatedAt = DateTime.UtcNow,
        };

        if (report.HasError)
            _logger.LogWarning(
                "[Processor] {Label}: proposal có {Err} ERROR validation — vẫn tạo kèm cảnh báo để không bỏ sót.",
                label, report.ErrorCount);

        if (doc.DryRun)
        {
            _logger.LogInformation(
                "[Processor] [DryRun] BỎ ghi DB. Proposal dự kiến: entity={Entity}, docNo={DocNo}, changed={C}, warn={W}, err={E}.",
                proposal.TargetEntityType, proposal.DocumentNo, diff.ChangedCount, report.WarningCount, report.ErrorCount);
            return IngestionOutcome.Proposal(null, report, diff);
        }

        var id = await _proposals.AddAsync(proposal, ct);
        _logger.LogInformation(
            "[Processor] {Label}: TẠO proposal #{Id} entity={Entity} docNo={DocNo}.",
            label, id, proposal.TargetEntityType, proposal.DocumentNo);
        return IngestionOutcome.Proposal(id, report, diff);
    }
}
