using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Watcher;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Ai;

/// <summary>
/// Discoverer rỗng — dùng khi thiếu API key. Luôn báo không khả dụng → mọi entity ra NeedsHuman.
/// Song song <see cref="OfflineComplianceExtractor"/>.
/// </summary>
public sealed class OfflineWebSearchDiscoverer : IWebSearchDiscoverer
{
    private readonly ILogger<OfflineWebSearchDiscoverer> _logger;

    public OfflineWebSearchDiscoverer(ILogger<OfflineWebSearchDiscoverer> logger)
    {
        _logger = logger;
        _logger.LogWarning("Dùng OfflineWebSearchDiscoverer (không LLM) — Kênh B sẽ chỉ tạo cảnh báo NeedsHuman.");
    }

    public Task<DiscoveryResult?> DiscoverNewerAsync(string targetEntityType, string? currentDocumentNo, CancellationToken ct)
        => throw new WebSearchUnavailableException("Web search không khả dụng (thiếu API key).");
}
