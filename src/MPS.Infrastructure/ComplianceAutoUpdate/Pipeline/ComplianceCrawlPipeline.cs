using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Ingestion;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Application.Options;
using MPS.Domain.Entities.Compliance.AutoUpdate;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Pipeline;

/// <summary>
/// Ghép luồng CRAWL (discovery): fingerprint danh sách → lọc văn bản chưa xử lý → tải toàn văn
/// → đẩy vào <see cref="IComplianceDocumentProcessor"/> (lõi dùng chung). Mọi giai đoạn bọc try-catch.
/// </summary>
public sealed class ComplianceCrawlPipeline : IComplianceCrawlPipeline
{
    private readonly IEnumerable<ILegalSourceAdapter> _adapters;
    private readonly IListFingerprintDetector _fingerprint;
    private readonly ICrawledDocumentTracker _tracker;
    private readonly IComplianceDocumentProcessor _processor;
    private readonly ComplianceAutoUpdateOptions _options;
    private readonly ILogger<ComplianceCrawlPipeline> _logger;

    public ComplianceCrawlPipeline(
        IEnumerable<ILegalSourceAdapter> adapters,
        IListFingerprintDetector fingerprint,
        ICrawledDocumentTracker tracker,
        IComplianceDocumentProcessor processor,
        ComplianceAutoUpdateOptions options,
        ILogger<ComplianceCrawlPipeline> logger)
    {
        _adapters = adapters;
        _fingerprint = fingerprint;
        _tracker = tracker;
        _processor = processor;
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
            // ---- Tải toàn văn (phần discovery riêng của crawl) ----
            var fullText = await adapter.FetchFullTextAsync(doc.Url, ct);
            if (string.IsNullOrWhiteSpace(fullText))
            {
                const string reason = "Toàn văn rỗng (nghi render JS).";
                _logger.LogWarning("[{RunId}] {Url}: {Reason} → Failed.", runId, doc.Url, reason);
                await SetStatus(doc.Url, ExtractStatuses.Failed, reason, ct);
                return DocOutcome.Failed;
            }

            // ---- Đẩy vào lõi dùng chung ----
            var outcome = await _processor.ProcessAsync(
                new RawDocument(adapter.SourceKey, doc.Url, doc.DocumentNo, fullText, _options.DryRun), ct);

            switch (outcome.Kind)
            {
                case IngestionKind.NoScopedContent:
                    await SetStatus(doc.Url, ExtractStatuses.Skipped, outcome.Reason, ct);
                    return DocOutcome.NoChange;

                case IngestionKind.Failed:
                    _logger.LogWarning("[{RunId}] {Url}: {Reason} → Failed.", runId, doc.Url, outcome.Reason);
                    await SetStatus(doc.Url, ExtractStatuses.Failed, outcome.Reason, ct);
                    return DocOutcome.Failed;

                case IngestionKind.NoChange:
                    await SetStatus(doc.Url, ExtractStatuses.NoChange, null, ct);
                    return DocOutcome.NoChange;

                case IngestionKind.Proposal:
                    await SetStatus(doc.Url, ExtractStatuses.ProposalCreated, null, ct);
                    _logger.LogInformation("[{RunId}] {Url}: tạo proposal #{Id}.", runId, doc.Url, outcome.ProposalId);
                    return DocOutcome.Proposal;

                default:
                    return DocOutcome.Failed;
            }
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
