using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;

namespace MPS.Infrastructure.BackgroundJobs;

/// <summary>
/// Runner cho pipeline tự động quét &amp; đề xuất cập nhật pháp luật.
///
/// Đợt này CHỈ chạy thủ công qua <see cref="RunOnceAsync"/> (gọi từ endpoint kích hoạt) để test.
/// Để chuyển sang background: cho class kế thừa <c>BackgroundService</c>, override <c>ExecuteAsync</c>
/// dùng <c>PeriodicTimer</c> gọi <see cref="RunOnceAsync"/> mỗi tick, rồi
/// <c>AddHostedService(sp =&gt; sp.GetRequiredService&lt;ComplianceAutoUpdateJob&gt;())</c> trong DI.
/// (Đồng pattern với ComplianceActivationJob / ContractAutoConvertJob.)
/// </summary>
public sealed class ComplianceAutoUpdateJob
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ComplianceAutoUpdateJob> _logger;

    public ComplianceAutoUpdateJob(IServiceScopeFactory scopeFactory, ILogger<ComplianceAutoUpdateJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Chạy pipeline một lần. Trả tóm tắt; null nếu đang có lần chạy khác (chống chạy chồng).</summary>
    public async Task<ComplianceCrawlSummary?> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogWarning("ComplianceAutoUpdateJob đang chạy — bỏ qua yêu cầu mới.");
            return null;
        }

        try
        {
            var runId = Guid.NewGuid();
            using var scope = _scopeFactory.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<IComplianceCrawlPipeline>();
            return await pipeline.ExecuteAsync(runId, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}
