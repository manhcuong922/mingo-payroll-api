using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Watcher;
using MPS.Application.Options;

namespace MPS.Infrastructure.BackgroundJobs;

/// <summary>
/// Kênh B — chạy watcher định kỳ (mặc định hằng tuần). Đồng pattern <see cref="ComplianceActivationJob"/>.
/// Cũng phục vụ "Dò ngay" qua <see cref="RunOnceAsync"/> (đăng ký Singleton + HostedService).
/// </summary>
public sealed class ComplianceWatcherJob : BackgroundService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ComplianceAutoUpdateOptions _options;
    private readonly ILogger<ComplianceWatcherJob> _logger;

    public ComplianceWatcherJob(
        IServiceScopeFactory scopeFactory, ComplianceAutoUpdateOptions options, ILogger<ComplianceWatcherJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Watcher.Enabled)
        {
            _logger.LogInformation("ComplianceWatcherJob: Watcher.Enabled=false — không chạy nền.");
            return;
        }

        var interval = TimeSpan.FromDays(Math.Max(1, _options.Watcher.IntervalDays));
        _logger.LogInformation("ComplianceWatcherJob started (mỗi {Days} ngày).", interval.TotalDays);

        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "ComplianceWatcherJob tick failed; retry next interval."); }

            try { await Task.Delay(interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    /// <summary>Chạy watcher một lần. Trả tóm tắt; null nếu đang có lần chạy khác.</summary>
    public async Task<ComplianceWatchSummary?> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogWarning("ComplianceWatcherJob đang chạy — bỏ qua yêu cầu mới.");
            return null;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var watcher = scope.ServiceProvider.GetRequiredService<IComplianceWatcher>();
            return await watcher.RunOnceAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}
