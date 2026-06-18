using System.Text.Json;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Application.Options;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Pipeline;

/// <summary>Ghép toàn bộ luồng. Mọi giai đoạn bọc try-catch để không làm chết caller.</summary>
public sealed class ComplianceCrawlPipeline : IComplianceCrawlPipeline
{
    private readonly IEnumerable<ILegalSourceAdapter> _adapters;
    private readonly IListFingerprintDetector _fingerprint;
    private readonly ICrawledDocumentTracker _tracker;
    private readonly IContentScopeFilter _scopeFilter;
    private readonly IComplianceExtractor _extractor;
    private readonly ISourceGroundingValidator _grounding;
    private readonly IComplianceLogicValidator _logic;
    private readonly IComplianceDiffService _diff;
    private readonly AppDbContext _db;
    private readonly ComplianceAutoUpdateOptions _options;
    private readonly ILogger<ComplianceCrawlPipeline> _logger;

    public ComplianceCrawlPipeline(
        IEnumerable<ILegalSourceAdapter> adapters,
        IListFingerprintDetector fingerprint,
        ICrawledDocumentTracker tracker,
        IContentScopeFilter scopeFilter,
        IComplianceExtractor extractor,
        ISourceGroundingValidator grounding,
        IComplianceLogicValidator logic,
        IComplianceDiffService diff,
        AppDbContext db,
        ComplianceAutoUpdateOptions options,
        ILogger<ComplianceCrawlPipeline> logger)
    {
        _adapters = adapters;
        _fingerprint = fingerprint;
        _tracker = tracker;
        _scopeFilter = scopeFilter;
        _extractor = extractor;
        _grounding = grounding;
        _logic = logic;
        _diff = diff;
        _db = db;
        _options = options;
        _logger = logger;
    }

    public async Task<ComplianceCrawlSummary> ExecuteAsync(Guid runId, CancellationToken ct)
    {
        var enabled = _adapters.Where(a => a.Enabled).ToList();
        _logger.LogInformation(
            "[{RunId}] BẮT ĐẦU run lúc {Now:o}. DryRun={DryRun}. Nguồn: {Sources}",
            runId, DateTime.UtcNow, _options.DryRun, string.Join(", ", enabled.Select(a => a.SourceKey)));

        int sourcesChecked = 0, newDocs = 0, proposals = 0, failures = 0;

        foreach (var adapter in enabled)
        {
            ct.ThrowIfCancellationRequested();
            sourcesChecked++;
            try
            {
                // ---- Cấp 1: fingerprint cả danh sách ----
                var listHtml = await adapter.FetchListHtmlAsync(ct);
                if (!await _fingerprint.HasChangedAsync(adapter.SourceKey, listHtml, ct))
                    continue;

                // ---- Cấp 2: lọc văn bản chưa xử lý ----
                var list = await adapter.FetchNewListAsync(ct);
                var fresh = new List<LegalDocumentRef>();
                foreach (var doc in list)
                    if (!await _tracker.IsProcessedAsync(doc.Url, ct))
                        fresh.Add(doc);

                _logger.LogInformation(
                    "[{RunId}] [Cấp 2] {Source}: tổng {Total}, đã xử lý {Skip}, mới {New}.",
                    runId, adapter.SourceKey, list.Count, list.Count - fresh.Count, fresh.Count);

                if (fresh.Count == 0) continue;

                foreach (var doc in fresh)
                {
                    ct.ThrowIfCancellationRequested();
                    var outcome = await ProcessDocumentAsync(runId, adapter, doc, ct);
                    newDocs++;
                    if (outcome == DocOutcome.Proposal) proposals++;
                    else if (outcome == DocOutcome.Failed) failures++;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                failures++;
                _logger.LogError(ex, "[{RunId}] Nguồn {Source} lỗi — bỏ qua, đi tiếp.", runId, adapter.SourceKey);
            }
        }

        _logger.LogInformation(
            "[{RunId}] KẾT THÚC run. sourcesChecked={Sources} newDocs={New} proposalsCreated={Prop} failures={Fail}",
            runId, sourcesChecked, newDocs, proposals, failures);

        return new ComplianceCrawlSummary(runId, sourcesChecked, newDocs, proposals, failures, _options.DryRun);
    }

    private enum DocOutcome { Proposal, NoChange, Failed }

    private async Task<DocOutcome> ProcessDocumentAsync(
        Guid runId, ILegalSourceAdapter adapter, LegalDocumentRef doc, CancellationToken ct)
    {
        // ghi Detected ngay (Cấp 2)
        if (!_options.DryRun) await _tracker.MarkDetectedAsync(doc, ct);

        try
        {
            // ---- Tải toàn văn ----
            var fullText = await adapter.FetchFullTextAsync(doc.Url, ct);
            if (string.IsNullOrWhiteSpace(fullText))
            {
                const string reason = "Toàn văn rỗng (nghi render JS).";
                _logger.LogWarning("[{RunId}] {Url}: {Reason} → Failed.", runId, doc.Url, reason);
                await SetStatus(doc.Url, ExtractStatuses.Failed, reason, ct);
                return DocOutcome.Failed;
            }

            // ---- Lọc 2 tầng nội dung ----
            var scoped = _scopeFilter.Filter(fullText);
            _logger.LogInformation(
                "[{RunId}] {Url}: lọc 2 tầng — {Total} điều → {Kept} điều trong scope.",
                runId, doc.Url, scoped.TotalClauses, scoped.Clauses.Count);

            if (!scoped.HasScopedContent)
            {
                await SetStatus(doc.Url, ExtractStatuses.Skipped, "Không có điều khoản trong scope.", ct);
                return DocOutcome.NoChange;
            }

            // ---- AI extract ----
            var result = await _extractor.ExtractAsync(scoped.ForLlm, ct);
            if (result == null)
            {
                await SetStatus(doc.Url, ExtractStatuses.Failed, "AI extract/parse lỗi.", ct);
                return DocOutcome.Failed;
            }
            result.DocumentNo ??= doc.DocumentNo;
            _logger.LogInformation("[{RunId}] {Url}: AI bóc {Count} field, entity={Entity}.",
                runId, doc.Url, result.Values.Count, result.TargetEntityType ?? "(?)");
            await SetStatus(doc.Url, ExtractStatuses.Extracted, null, ct);

            // ---- 3 lớp validation (tuần tự, độc lập) ----
            var report = new ValidationReport();
            _grounding.Validate(result, scoped.SourceText, report);
            _logic.Validate(result, report);
            var diff = await _diff.ComputeDiffAsync(result, report, ct);

            // ---- Không khác biệt → không tạo proposal ----
            if (!diff.HasSubstantiveChange)
            {
                _logger.LogInformation("[{RunId}] {Url}: data đã trùng — NoChange, không tạo proposal.", runId, doc.Url);
                await SetStatus(doc.Url, ExtractStatuses.NoChange, null, ct);
                return DocOutcome.NoChange;
            }

            // ---- Tạo proposal (PendingReview) ----
            var proposal = new ComplianceUpdateProposal
            {
                SourceKey = adapter.SourceKey,
                DocumentUrl = doc.Url,
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
                    "[{RunId}] {Url}: proposal có {Err} ERROR validation — vẫn tạo kèm cảnh báo để không bỏ sót.",
                    runId, doc.Url, report.ErrorCount);

            if (_options.DryRun)
            {
                _logger.LogInformation(
                    "[{RunId}] [DryRun] BỎ ghi DB. Proposal dự kiến: entity={Entity}, docNo={DocNo}, changed={C}, warn={W}, err={E}.",
                    runId, proposal.TargetEntityType, proposal.DocumentNo, diff.ChangedCount,
                    report.WarningCount, report.ErrorCount);
                return DocOutcome.Proposal;
            }

            _db.ComplianceUpdateProposals.Add(proposal);
            await _db.SaveChangesAsync(ct);
            await SetStatus(doc.Url, ExtractStatuses.ProposalCreated, null, ct);

            _logger.LogInformation(
                "[{RunId}] {Url}: TẠO proposal #{Id} entity={Entity} docNo={DocNo} status={Status}.",
                runId, doc.Url, proposal.Id, proposal.TargetEntityType, proposal.DocumentNo, proposal.Status);
            return DocOutcome.Proposal;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RunId}] {Url}: lỗi xử lý văn bản.", runId, doc.Url);
            await SetStatus(doc.Url, ExtractStatuses.Failed, ex.Message, ct);
            return DocOutcome.Failed;
        }
    }

    private async Task SetStatus(string url, string status, string? reason, CancellationToken ct)
    {
        if (_options.DryRun) return;   // DryRun: không ghi DB
        await _tracker.UpdateStatusAsync(url, status, reason, ct);
    }
}
