using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Compliance.AutoUpdate.Ingestion;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.AutoUpdate.Watcher;

/// <summary>Tóm tắt một lần chạy watcher.</summary>
public sealed record ComplianceWatchSummary(int Checked, int Proposals, int NeedsHuman, int NoChange);

/// <summary>Kênh B — rà soát chủ động từng loại entity compliance.</summary>
public interface IComplianceWatcher
{
    Task<ComplianceWatchSummary> RunOnceAsync(CancellationToken ct);
}

/// <summary>
/// Với mỗi <see cref="ComplianceAuthoritativeSource"/>: hỏi LLM (web search) có bản mới hơn không →
/// nếu có URL thì đẩy vào lõi (grounding thật) → tạo proposal; không tự xác minh được → Lead NeedsHuman.
/// </summary>
public sealed class ComplianceWatcher : IComplianceWatcher
{
    private const string SourceKey = "watcher-websearch";

    private readonly IComplianceAuthoritativeSourceRepository _authoritative;
    private readonly IWebSearchDiscoverer _discoverer;
    private readonly IDocumentTextFetcher _fetcher;
    private readonly IComplianceDocumentProcessor _processor;
    private readonly IComplianceLeadRepository _leads;
    private readonly IClock _clock;
    private readonly ILogger<ComplianceWatcher> _logger;

    public ComplianceWatcher(
        IComplianceAuthoritativeSourceRepository authoritative,
        IWebSearchDiscoverer discoverer,
        IDocumentTextFetcher fetcher,
        IComplianceDocumentProcessor processor,
        IComplianceLeadRepository leads,
        IClock clock,
        ILogger<ComplianceWatcher> logger)
    {
        _authoritative = authoritative;
        _discoverer = discoverer;
        _fetcher = fetcher;
        _processor = processor;
        _leads = leads;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ComplianceWatchSummary> RunOnceAsync(CancellationToken ct)
    {
        var sources = await _authoritative.GetAllAsync(ct);
        _logger.LogInformation("[Watcher] BẮT ĐẦU rà soát {Count} loại entity.", sources.Count);

        int checkd = 0, proposals = 0, needsHuman = 0, noChange = 0;

        foreach (var src in sources)
        {
            ct.ThrowIfCancellationRequested();
            checkd++;
            try
            {
                DiscoveryResult? discovery;
                try
                {
                    discovery = await _discoverer.DiscoverNewerAsync(src.TargetEntityType, src.DocumentNo, ct);
                }
                catch (WebSearchUnavailableException ex)
                {
                    await AddLeadAsync(src.TargetEntityType, null, LeadStatuses.NeedsHuman, null,
                        $"Web search không khả dụng — cần kiểm tra thủ công. ({ex.Message})", ct);
                    needsHuman++;
                    continue;
                }

                // Không có bản mới hơn → coi như đã xác minh, cập nhật mốc; không tạo Lead (tránh nhiễu).
                if (discovery is null)
                {
                    await ReVerifyAsync(src, ct);
                    noChange++;
                    continue;
                }

                // Có nghi ngờ nhưng thiếu URL → cần người tra.
                if (string.IsNullOrWhiteSpace(discovery.SourceUrl))
                {
                    await AddLeadAsync(src.TargetEntityType, discovery, LeadStatuses.NeedsHuman, null,
                        "LLM nghi có bản mới nhưng không cung cấp URL nguồn.", ct);
                    needsHuman++;
                    continue;
                }

                var fullText = await _fetcher.FromUrlAsync(discovery.SourceUrl, ct);
                if (string.IsNullOrWhiteSpace(fullText))
                {
                    await AddLeadAsync(src.TargetEntityType, discovery, LeadStatuses.NeedsHuman, null,
                        "Không tải được nội dung từ URL (nghi PDF scan/trang JS hoặc URL hỏng).", ct);
                    needsHuman++;
                    continue;
                }

                var outcome = await _processor.ProcessAsync(
                    new RawDocument(SourceKey, discovery.SourceUrl, discovery.DocumentNo, fullText), ct);

                switch (outcome.Kind)
                {
                    case IngestionKind.Proposal:
                        await AddLeadAsync(src.TargetEntityType, discovery, LeadStatuses.Proposal, outcome.ProposalId,
                            "Đã grounding URL nguồn → tạo đề xuất chờ duyệt.", ct);
                        proposals++;
                        break;

                    case IngestionKind.NoChange:
                        await ReVerifyAsync(src, ct);
                        await AddLeadAsync(src.TargetEntityType, discovery, LeadStatuses.NoChange, null,
                            "Đã kiểm chứng URL — số liệu trùng dữ liệu đang áp dụng.", ct);
                        noChange++;
                        break;

                    default: // NoScopedContent | Failed
                        await AddLeadAsync(src.TargetEntityType, discovery, LeadStatuses.NeedsHuman, null,
                            outcome.Reason ?? "Không bóc được số liệu từ URL.", ct);
                        needsHuman++;
                        break;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Watcher] {Entity}: lỗi rà soát — bỏ qua, đi tiếp.", src.TargetEntityType);
            }
        }

        _logger.LogInformation(
            "[Watcher] KẾT THÚC. checked={Checked} proposals={Prop} needsHuman={NH} noChange={NC}.",
            checkd, proposals, needsHuman, noChange);
        return new ComplianceWatchSummary(checkd, proposals, needsHuman, noChange);
    }

    private async Task ReVerifyAsync(ComplianceAuthoritativeSource src, CancellationToken ct) =>
        await _authoritative.UpsertAsync(
            src.TargetEntityType, src.LegalDocumentId, src.DocumentNo, src.DocumentUrl, _clock.UtcNow, ct);

    private Task AddLeadAsync(
        string entityType, DiscoveryResult? d, string status, int? proposalId, string note, CancellationToken ct)
    {
        var resolved = status is LeadStatuses.Proposal or LeadStatuses.NoChange;
        var lead = new ComplianceLead
        {
            TargetEntityType = entityType,
            SourceKey = SourceKey,
            ClaimedDocumentNo = d?.DocumentNo,
            ClaimedEffectiveDate = d?.EffectiveDate,
            ClaimedSourceUrl = d?.SourceUrl,
            ClaimSummary = d?.Summary,
            Status = status,
            ProposalId = proposalId,
            Note = note,
            CreatedAt = _clock.UtcNow,
            ResolvedAt = resolved ? _clock.UtcNow : null,
        };
        return _leads.AddAsync(lead, ct);
    }
}
