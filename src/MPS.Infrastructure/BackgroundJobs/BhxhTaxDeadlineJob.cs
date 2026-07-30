using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily tick: quét mọi nghĩa vụ BHXH/TNCN đang <see cref="PaymentObligationStatus.Pending"/> hoặc
/// <see cref="PaymentObligationStatus.PartiallyPaid"/> có <c>Deadline &lt; hôm nay (UTC)</c> và
/// chuyển sang <see cref="PaymentObligationStatus.Overdue"/>. Không đụng obligation Completed.
/// Không tính tiền phạt — chỉ đổi cờ để UI cảnh báo.
/// </summary>
public sealed class BhxhTaxDeadlineJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BhxhTaxDeadlineJob> _logger;

    public BhxhTaxDeadlineJob(IServiceScopeFactory scopeFactory, ILogger<BhxhTaxDeadlineJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BhxhTaxDeadlineJob started; interval={Interval}.", Interval);
        try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BhxhTaxDeadlineJob tick failed; retry next interval.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogDebug("BhxhTaxDeadlineJob already running; skipping.");
            return 0;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var repo = scope.ServiceProvider.GetRequiredService<IPaymentObligationRepository>();

            var today = clock.UtcNow.Date;
            var overdueCandidates = await repo.ListPendingOrPartiallyPaidWithDeadlineBeforeAsync(today, cancellationToken);
            if (overdueCandidates.Count == 0)
            {
                _logger.LogDebug("BhxhTaxDeadlineJob: no pending obligation past deadline.");
                return 0;
            }

            foreach (var obligation in overdueCandidates)
            {
                obligation.Status = PaymentObligationStatus.Overdue;
                obligation.UpdatedAt = clock.UtcNow;
            }

            await repo.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "BhxhTaxDeadlineJob: đánh cờ Overdue cho {Count} nghĩa vụ.", overdueCandidates.Count);
            return overdueCandidates.Count;
        }
        finally
        {
            _gate.Release();
        }
    }
}
