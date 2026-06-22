using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Infrastructure.ComplianceAutoUpdate.Common;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Crawl;

/// <summary>Cấp 2 — chống trùng dựa trên UrlHash trong CrawledDocumentLog.</summary>
public sealed class CrawledDocumentTracker : ICrawledDocumentTracker
{
    private readonly AppDbContext _db;
    private readonly ILogger<CrawledDocumentTracker> _logger;

    public CrawledDocumentTracker(AppDbContext db, ILogger<CrawledDocumentTracker> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> IsProcessedAsync(string url, CancellationToken ct)
    {
        var hash = Hashing.Sha256Hex(url);
        return await _db.ComplianceCrawledDocumentLogs.AnyAsync(c => c.UrlHash == hash, ct);
    }

    public async Task MarkDetectedAsync(LegalDocumentRef doc, CancellationToken ct)
    {
        var hash = Hashing.Sha256Hex(doc.Url);
        if (await _db.ComplianceCrawledDocumentLogs.AnyAsync(c => c.UrlHash == hash, ct)) return;

        _db.ComplianceCrawledDocumentLogs.Add(new CrawledDocumentLog
        {
            SourceKey = doc.SourceKey,
            DocumentUrl = doc.Url,
            UrlHash = hash,
            DocumentNo = doc.DocumentNo,
            Title = doc.Title,
            ExtractStatus = ExtractStatuses.Detected,
            FirstSeenAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("[Cấp 2] Đánh dấu Detected: {Url}", doc.Url);
    }

    public async Task UpdateStatusAsync(string url, string status, string? failureReason, CancellationToken ct)
    {
        var hash = Hashing.Sha256Hex(url);
        var log = await _db.ComplianceCrawledDocumentLogs.FirstOrDefaultAsync(c => c.UrlHash == hash, ct);
        if (log == null)
        {
            _logger.LogWarning("Cập nhật status nhưng không thấy log cho {Url}", url);
            return;
        }
        log.ExtractStatus = status;
        log.FailureReason = failureReason;
        log.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
