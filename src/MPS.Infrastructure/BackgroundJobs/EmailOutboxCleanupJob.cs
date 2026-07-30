using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Email;

namespace MPS.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily UTC-midnight job — xóa các <see cref="EmailOutboxMessage"/> đã gửi (Sent) cũ hơn
/// <c>EmailDispatch:SentRetentionDays</c> để bảng outbox không phình. Cùng khuôn với
/// <see cref="AuditLogRetentionJob"/> (SemaphoreSlim + RunOnceAsync + tick nửa đêm).
/// </summary>
public sealed class EmailOutboxCleanupJob : BackgroundService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EmailDispatchOptions _options;
    private readonly ILogger<EmailOutboxCleanupJob> _logger;

    public EmailOutboxCleanupJob(
        IServiceScopeFactory scopeFactory,
        IOptions<EmailDispatchOptions> options,
        ILogger<EmailOutboxCleanupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailOutboxCleanupJob started; retention={Days}d.", _options.SentRetentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var delay = now.Date.AddDays(1) - now; // tới nửa đêm UTC kế tiếp

            try { await Task.Delay(delay, stoppingToken); }
            catch (TaskCanceledException) { break; }

            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmailOutboxCleanupJob tick failed; retry next midnight.");
            }
        }
    }

    /// <summary>Xóa các dòng Sent cũ. Trả về số dòng đã xóa.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogDebug("EmailOutboxCleanupJob already running; skipping.");
            return 0;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IEmailOutboxRepository>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            var cutoff = clock.UtcNow.AddDays(-Math.Max(0, _options.SentRetentionDays));
            var deleted = await repo.DeleteSentBeforeAsync(cutoff, cancellationToken);

            if (deleted > 0)
                _logger.LogInformation("EmailOutboxCleanupJob: deleted {Count} sent messages (cutoff={Cutoff:O}).", deleted, cutoff);
            else
                _logger.LogDebug("EmailOutboxCleanupJob: nothing to delete.");
            return deleted;
        }
        finally
        {
            _gate.Release();
        }
    }
}
