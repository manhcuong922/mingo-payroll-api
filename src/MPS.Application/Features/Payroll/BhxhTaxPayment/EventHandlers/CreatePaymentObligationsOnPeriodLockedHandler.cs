using MediatR;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Models.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Entities.Payroll.BhxhTax;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.EventHandlers;

/// <summary>
/// Handler cho <see cref="PayrollPeriodLockedNotification"/>: đọc cell Total row của kỳ vừa khoá,
/// sinh 2 nghĩa vụ nộp (BHXH gộp: <c>CONG_BH_DN + CONG_BH_NLD + KPCD</c>; TNCN: <c>THUETNCN</c>).
///
/// <b>Idempotent</b>: nếu 2 nghĩa vụ đã tồn tại cho kỳ (re-lock, retry), handler skip. Error-isolation
/// theo pattern hiện tại: swallow exception + log, không throw ngược làm fail lệnh khoá gốc.
/// </summary>
public sealed class CreatePaymentObligationsOnPeriodLockedHandler
    : INotificationHandler<PayrollPeriodLockedNotification>
{
    private readonly IPayrollPeriodRepository _periods;
    private readonly IPaymentObligationRepository _obligations;
    private readonly ILogger<CreatePaymentObligationsOnPeriodLockedHandler> _logger;

    public CreatePaymentObligationsOnPeriodLockedHandler(
        IPayrollPeriodRepository periods,
        IPaymentObligationRepository obligations,
        ILogger<CreatePaymentObligationsOnPeriodLockedHandler> logger)
    {
        _periods = periods;
        _obligations = obligations;
        _logger = logger;
    }

    public async Task Handle(PayrollPeriodLockedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            var existing = await _obligations.GetByPeriodAsync(notification.PeriodId, cancellationToken);
            var missing = new List<PaymentObligation>(2);

            var bhxhAmount = await ComputeBhxhAmountAsync(notification.PeriodId, cancellationToken);
            var tncnAmount = await ComputeTncnAmountAsync(notification.PeriodId, cancellationToken);

            if (!existing.Any(o => o.ObligationType == PaymentObligationType.Bhxh))
            {
                missing.Add(new PaymentObligation
                {
                    PayrollPeriodId = notification.PeriodId,
                    ObligationType = PaymentObligationType.Bhxh,
                    AmountDue = bhxhAmount,
                    Deadline = BhxhTaxDeadlineCalculator.For(
                        PaymentObligationType.Bhxh, notification.PeriodYear, notification.PeriodMonth),
                    Status = PaymentObligationStatus.Pending,
                    CreatedAt = notification.LockedAtUtc,
                    CreatedByUserId = notification.LockedByUserId,
                });
            }

            if (!existing.Any(o => o.ObligationType == PaymentObligationType.Tncn))
            {
                missing.Add(new PaymentObligation
                {
                    PayrollPeriodId = notification.PeriodId,
                    ObligationType = PaymentObligationType.Tncn,
                    AmountDue = tncnAmount,
                    Deadline = BhxhTaxDeadlineCalculator.For(
                        PaymentObligationType.Tncn, notification.PeriodYear, notification.PeriodMonth),
                    Status = PaymentObligationStatus.Pending,
                    CreatedAt = notification.LockedAtUtc,
                    CreatedByUserId = notification.LockedByUserId,
                });
            }

            if (missing.Count == 0)
            {
                _logger.LogDebug(
                    "PayrollPeriod #{PeriodId} đã có đủ nghĩa vụ BHXH & TNCN — skip.", notification.PeriodId);
                return;
            }

            await _obligations.AddRangeAsync(missing, cancellationToken);
            await _obligations.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Sinh {Count} nghĩa vụ nộp cho PayrollPeriod #{PeriodId} (BHXH={Bhxh}, TNCN={Tncn}).",
                missing.Count, notification.PeriodId, bhxhAmount, tncnAmount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Không thể sinh nghĩa vụ BHXH/TNCN cho PayrollPeriod #{PeriodId}. Cần chạy tay hoặc re-lock.",
                notification.PeriodId);
        }
    }

    private async Task<decimal> ComputeBhxhAmountAsync(int periodId, CancellationToken ct)
    {
        var period = await _periods.GetFullAsync(periodId, asNoTracking: true, ct);
        if (period is null) return 0m;
        var totalRow = period.Rows.FirstOrDefault(r => r.RowType == PayrollRowType.Total);
        if (totalRow is null) return 0m;

        var bhxhDn = EffectiveValue(totalRow, PayrollColumnCodes.CongBhDn);
        var bhxhNld = EffectiveValue(totalRow, PayrollColumnCodes.CongBhNld);
        var kpcd = EffectiveValue(totalRow, PayrollColumnCodes.Kpcd);
        return Math.Round(bhxhDn + bhxhNld + kpcd, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<decimal> ComputeTncnAmountAsync(int periodId, CancellationToken ct)
    {
        var period = await _periods.GetFullAsync(periodId, asNoTracking: true, ct);
        if (period is null) return 0m;
        var totalRow = period.Rows.FirstOrDefault(r => r.RowType == PayrollRowType.Total);
        if (totalRow is null) return 0m;

        var tncn = EffectiveValue(totalRow, PayrollColumnCodes.ThueTncn);
        return Math.Round(tncn, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal EffectiveValue(PayrollRow row, string columnCode)
    {
        var cell = row.Cells.FirstOrDefault(c =>
            string.Equals(c.ColumnCode, columnCode, StringComparison.OrdinalIgnoreCase));
        if (cell is null) return 0m;
        return (cell.IsOverridden ? cell.OverrideValue : cell.ComputedValue) ?? 0m;
    }
}
