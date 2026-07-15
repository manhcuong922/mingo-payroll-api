using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces;

namespace MPS.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily tick: HĐ Active có EffectiveTo &lt; NOW và đã có HĐ con (ParentContractId trỏ về)
/// → set Status=Expired. HĐ không có HĐ con sẽ do AutoConvertJob xử lý sau 30 ngày.
/// </summary>
public sealed class ContractExpiryJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContractExpiryJob> _logger;

    public ContractExpiryJob(IServiceScopeFactory scopeFactory, ILogger<ContractExpiryJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ContractExpiryJob started; interval={Interval}.", Interval);
        try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ContractExpiryJob tick failed; retry next interval.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogDebug("ContractExpiryJob already running; skipping.");
            return 0;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var repo = scope.ServiceProvider.GetRequiredService<IContractRepository>();

            var (expired, affectedEmployeeIds) = await repo.ExpireOverdueContractsAsync(clock.UtcNow, cancellationToken);
            if (expired == 0)
            {
                _logger.LogDebug("ContractExpiryJob: nothing to expire.");
                return 0;
            }

            _logger.LogInformation("ContractExpiryJob: expired {Count} overdue contracts.", expired);

            // Sau khi expire HĐ: inactive user account của NLĐ không còn HĐ Active nào.
            // Nếu NLĐ vẫn còn HĐ khác đang Active (VD: HĐ mới ký, HĐ song song) thì repo tự bỏ qua.
            if (affectedEmployeeIds.Count > 0)
            {
                var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var deactivated = await userRepo.DeactivateForEmployeesWithoutActiveContractAsync(
                    affectedEmployeeIds, clock.UtcNow, cancellationToken);
                if (deactivated > 0)
                    _logger.LogInformation(
                        "ContractExpiryJob: deactivated {Count} user account(s) whose employees no longer have any active contract.",
                        deactivated);
            }

            return expired;
        }
        finally
        {
            _gate.Release();
        }
    }
}
