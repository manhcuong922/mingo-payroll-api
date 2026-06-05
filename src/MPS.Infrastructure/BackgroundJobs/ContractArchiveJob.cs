using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces;

namespace MPS.Infrastructure.BackgroundJobs;

/// <summary>
/// Monthly tick: archive HĐ đã Terminated/Expired hơn 10 năm
/// (Luật Lưu trữ + Điều 7 NĐ 152/2020). Sau khi archive, mọi write trên HĐ
/// sẽ bị <see cref="MPS.Application.Behaviors.ArchivedContractGuardBehavior"/> chặn → 409.
/// </summary>
public sealed class ContractArchiveJob : BackgroundService
{
    // Chạy mỗi 24h thay vì 30 ngày — overhead vài ms khi không có row nào match,
    // mà tránh trễ archive nếu có HĐ rơi vào ngưỡng giữa lần chạy.
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContractArchiveJob> _logger;

    public ContractArchiveJob(IServiceScopeFactory scopeFactory, ILogger<ContractArchiveJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ContractArchiveJob started; interval={Interval}.", Interval);
        try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ContractArchiveJob tick failed; retry next interval.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogDebug("ContractArchiveJob already running; skipping.");
            return 0;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var repo = scope.ServiceProvider.GetRequiredService<IContractRepository>();

            var archived = await repo.ArchiveOldContractsAsync(clock.UtcNow, cancellationToken);
            if (archived > 0)
                _logger.LogInformation("ContractArchiveJob: archived {Count} long-retired contracts.", archived);
            else
                _logger.LogDebug("ContractArchiveJob: nothing to archive.");
            return archived;
        }
        finally
        {
            _gate.Release();
        }
    }
}
