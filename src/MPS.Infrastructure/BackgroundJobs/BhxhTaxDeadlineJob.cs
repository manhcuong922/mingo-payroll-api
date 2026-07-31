using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily tick:
///   1. Mark Overdue: mọi nghĩa vụ Pending/PartiallyPaid có <c>Deadline &lt; hôm nay</c> chuyển sang Overdue.
///   2. Phase 4 §N9 — publish TB "sắp đến hạn" cho T-7/T-3/T-1 và "đã quá hạn" hằng ngày.
///
/// Publish là best-effort — fail không rollback việc mark Overdue. Không tính tiền phạt.
/// </summary>
public sealed class BhxhTaxDeadlineJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly int[] SoonThresholds = { 7, 3, 1 }; // §N9
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
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

            var today = clock.UtcNow.Date;

            // Gộp 1 query: mọi obligation chưa Completed có deadline trong khoảng (-∞, today+8)
            // → đủ dữ liệu cho cả nhánh Overdue (deadline < today) và Soon (deadline trong 1..7 ngày tới).
            var candidates = await repo.ListPendingOrPartiallyPaidWithDeadlineBeforeAsync(
                today.AddDays(SoonThresholds[0] + 1), cancellationToken);
            if (candidates.Count == 0)
            {
                _logger.LogDebug("BhxhTaxDeadlineJob: no obligation in the deadline window.");
                return 0;
            }

            var overdueMarked = 0;
            foreach (var obligation in candidates)
            {
                var deadlineDate = obligation.Deadline.Date;

                if (deadlineDate < today)
                {
                    if (obligation.Status != PaymentObligationStatus.Overdue)
                    {
                        obligation.Status = PaymentObligationStatus.Overdue;
                        obligation.UpdatedAt = clock.UtcNow;
                        overdueMarked++;
                    }
                    await PublishOverdueAsync(publisher, obligation, today, cancellationToken);
                }
                else
                {
                    var daysRemaining = (deadlineDate - today).Days;
                    if (SoonThresholds.Contains(daysRemaining))
                        await PublishSoonAsync(publisher, obligation, daysRemaining, cancellationToken);
                }
            }

            if (overdueMarked > 0)
                await repo.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "BhxhTaxDeadlineJob: scanned {Count}, marked Overdue {OverdueMarked}.",
                candidates.Count, overdueMarked);
            return candidates.Count;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static Task PublishSoonAsync(
        IPublisher publisher,
        MPS.Domain.Entities.Payroll.BhxhTax.PaymentObligation obligation,
        int daysRemaining,
        CancellationToken ct) =>
        publisher.Publish(new BhxhTaxDeadlineSoonNotification(
            ObligationId: obligation.Id,
            ObligationLabel: LabelOf(obligation),
            DueDate: obligation.Deadline,
            DaysRemaining: daysRemaining,
            Amount: obligation.AmountDue,
            Currency: "VND"), ct);

    private static Task PublishOverdueAsync(
        IPublisher publisher,
        MPS.Domain.Entities.Payroll.BhxhTax.PaymentObligation obligation,
        DateTime today,
        CancellationToken ct) =>
        publisher.Publish(new BhxhTaxOverdueNotification(
            ObligationId: obligation.Id,
            ObligationLabel: LabelOf(obligation),
            DueDate: obligation.Deadline,
            DaysOverdue: (today - obligation.Deadline.Date).Days,
            Amount: obligation.AmountDue,
            Currency: "VND"), ct);

    private static string LabelOf(MPS.Domain.Entities.Payroll.BhxhTax.PaymentObligation o) =>
        o.ObligationType == PaymentObligationType.Bhxh
            ? $"BHXH kỳ {o.PayrollPeriod?.PeriodMonth:D2}/{o.PayrollPeriod?.PeriodYear}"
            : $"TNCN kỳ {o.PayrollPeriod?.PeriodMonth:D2}/{o.PayrollPeriod?.PeriodYear}";
}
