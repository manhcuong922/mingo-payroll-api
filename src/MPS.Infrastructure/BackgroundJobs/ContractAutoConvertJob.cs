using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces;

namespace MPS.Infrastructure.BackgroundJobs;

/// <summary>
/// Điều 20 k.2.b BLLĐ 2019 — daily tick: HĐ Definite hết hạn quá 30 ngày mà chưa ký mới
/// → auto-convert sang Indefinite. SemaphoreSlim chống overlap nếu lần chạy trước chưa xong.
/// </summary>
public sealed class ContractAutoConvertJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContractAutoConvertJob> _logger;

    public ContractAutoConvertJob(IServiceScopeFactory scopeFactory, ILogger<ContractAutoConvertJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ContractAutoConvertJob started; interval={Interval}.", Interval);
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ContractAutoConvertJob tick failed; retry next interval.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    /// <summary>
    /// Public entry-point — also invoked by admin manual-trigger endpoint.
    /// SemaphoreSlim ensures only 1 execution at a time.
    /// </summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogDebug("ContractAutoConvertJob already running; skipping this trigger.");
            return 0;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var repo = scope.ServiceProvider.GetRequiredService<IContractRepository>();

            var converted = await repo.AutoConvertExpiredDefiniteAsync(clock.UtcNow, cancellationToken);
            if (converted > 0)
                _logger.LogInformation("ContractAutoConvertJob: converted {Count} expired-definite contracts.", converted);
            else
                _logger.LogDebug("ContractAutoConvertJob: no contracts to convert.");
            return converted;
        }
        finally
        {
            _gate.Release();
        }
    }
}
