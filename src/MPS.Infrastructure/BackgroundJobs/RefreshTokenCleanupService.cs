using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MPS.Domain.Interfaces;

namespace MPS.Infrastructure.BackgroundJobs;

public sealed class RefreshTokenCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RefreshTokenCleanupService> _logger;

    public RefreshTokenCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<RefreshTokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RefreshTokenCleanupService started. Interval: {Interval}, retention: {Retention}.",
            Interval, RetentionPeriod);

        // Initial delay so startup logs aren't drowned out.
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh token cleanup failed; will retry on next tick.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - RetentionPeriod;

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

        var deleted = await repo.DeleteExpiredAsync(cutoff, cancellationToken);
        if (deleted > 0)
            _logger.LogInformation("Deleted {Count} expired refresh tokens older than {Cutoff:u}.", deleted, cutoff);
        else
            _logger.LogDebug("No expired refresh tokens to delete (cutoff {Cutoff:u}).", cutoff);
    }
}
