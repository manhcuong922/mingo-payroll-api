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
/// Handler cho <see cref="PayrollPeriodLockedNotification"/> (v3 line-level):
/// - Duyệt Employee rows của kỳ, trích snapshot đóng góp BHXH & TNCN per NV.
/// - Tạo 2 obligation cha (BHXH + TNCN) + N line con mỗi obligation (skip NV amount = 0).
/// - <c>AmountDue</c> cha = sum(lines.TotalAmount).
///
/// <b>Idempotent trên re-lock</b>:
/// - Nếu obligation <c>Pending</c> và không có line Paid/Excluded → xoá lines cũ + tạo lại + update AmountDue.
/// - Nếu có bất kỳ line Paid/Excluded, hoặc status ∈ {PartiallyPaid, Completed} → skip + log warning.
///
/// <b>Error isolation</b>: try/catch swallow — không throw ngược để không fail lệnh Lock gốc.
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
            var period = await _periods.GetFullAsync(notification.PeriodId, asNoTracking: true, cancellationToken);
            if (period is null)
            {
                _logger.LogWarning("PayrollPeriod #{PeriodId} không tồn tại — skip sinh nghĩa vụ.", notification.PeriodId);
                return;
            }

            var existing = await _obligations.GetByPeriodAsync(notification.PeriodId, cancellationToken);

            foreach (var type in new[] { PaymentObligationType.Bhxh, PaymentObligationType.Tncn })
            {
                var lineDrafts = BuildLineDrafts(period, type);
                var totalAmount = lineDrafts.Sum(l => l.EmployeeContribution + l.EmployerContribution);

                var current = existing.FirstOrDefault(o => o.ObligationType == type);
                if (current is null)
                {
                    var obligation = new PaymentObligation
                    {
                        PayrollPeriodId = notification.PeriodId,
                        ObligationType = type,
                        AmountDue = totalAmount,
                        Deadline = BhxhTaxDeadlineCalculator.For(
                            type, notification.PeriodYear, notification.PeriodMonth),
                        Status = PaymentObligationStatus.Pending,
                        CreatedAt = notification.LockedAtUtc,
                        CreatedByUserId = notification.LockedByUserId,
                    };
                    await _obligations.AddRangeAsync(new[] { obligation }, cancellationToken);
                    await _obligations.SaveChangesAsync(cancellationToken);

                    foreach (var draft in lineDrafts)
                    {
                        draft.PaymentObligationId = obligation.Id;
                    }
                    await _obligations.AddLinesAsync(lineDrafts, cancellationToken);
                    await _obligations.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Sinh nghĩa vụ {Type} cho PayrollPeriod #{PeriodId} với {LineCount} line, AmountDue={AmountDue}.",
                        type, notification.PeriodId, lineDrafts.Count, totalAmount);
                    continue;
                }

                // Re-lock scenario.
                if (current.Status is PaymentObligationStatus.PartiallyPaid or PaymentObligationStatus.Completed)
                {
                    _logger.LogWarning(
                        "PayrollPeriod #{PeriodId} re-lock: nghĩa vụ {Type} đã ở trạng thái {Status} — skip.",
                        notification.PeriodId, type, current.Status);
                    continue;
                }

                var counts = await _obligations.CountLinesByStatusAsync(current.Id, cancellationToken);
                if (counts.Paid > 0 || counts.Excluded > 0)
                {
                    _logger.LogWarning(
                        "PayrollPeriod #{PeriodId} re-lock: nghĩa vụ {Type} đã có line Paid/Excluded — skip.",
                        notification.PeriodId, type);
                    continue;
                }

                // Safe to wipe + recreate.
                var tracked = await _obligations.GetByIdAsync(current.Id, asNoTracking: false, cancellationToken);
                if (tracked is null) continue;

                await _obligations.DeleteLinesByObligationAsync(tracked.Id, cancellationToken);
                await _obligations.SaveChangesAsync(cancellationToken);

                tracked.AmountDue = totalAmount;
                tracked.Status = PaymentObligationStatus.Pending;
                tracked.UpdatedAt = notification.LockedAtUtc;
                tracked.UpdatedByUserId = notification.LockedByUserId;

                foreach (var draft in lineDrafts)
                {
                    draft.PaymentObligationId = tracked.Id;
                }
                await _obligations.AddLinesAsync(lineDrafts, cancellationToken);
                await _obligations.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "PayrollPeriod #{PeriodId} re-lock: refresh nghĩa vụ {Type} với {LineCount} line, AmountDue={AmountDue}.",
                    notification.PeriodId, type, lineDrafts.Count, totalAmount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Không thể sinh nghĩa vụ BHXH/TNCN cho PayrollPeriod #{PeriodId}. Cần chạy tay hoặc re-lock.",
                notification.PeriodId);
        }
    }

    private static List<PaymentObligationLine> BuildLineDrafts(PayrollPeriod period, PaymentObligationType type)
    {
        var result = new List<PaymentObligationLine>();
        foreach (var row in period.Rows)
        {
            if (row.RowType != PayrollRowType.Employee) continue;
            if (row.EmployeeInformationId is null) continue;
            if (row.EmployeeInformation is null) continue;

            var (employeeContribution, employerContribution) = ExtractAmounts(row, type);
            if (employeeContribution == 0m && employerContribution == 0m) continue;

            var employeeContributionRounded = Math.Round(employeeContribution, 2, MidpointRounding.AwayFromZero);
            var employerContributionRounded = Math.Round(employerContribution, 2, MidpointRounding.AwayFromZero);

            var employeeCode = row.EmployeeInformation.User?.EmployeeCode ?? string.Empty;
            var employeeName = row.EmployeeInformation.FullName ?? string.Empty;

            result.Add(new PaymentObligationLine
            {
                EmployeeInformationId = row.EmployeeInformationId.Value,
                EmployeeCodeSnapshot = Truncate(employeeCode, 50),
                EmployeeFullNameSnapshot = Truncate(employeeName, 200),
                EmployeeContribution = employeeContributionRounded,
                EmployerContribution = employerContributionRounded,
                LineStatus = PaymentLineStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            });
        }
        return result;
    }

    private static (decimal Employee, decimal Employer) ExtractAmounts(PayrollRow row, PaymentObligationType type)
    {
        switch (type)
        {
            case PaymentObligationType.Bhxh:
                var bhxhNld = EffectiveValue(row, PayrollColumnCodes.CongBhNld);
                var bhxhDn = EffectiveValue(row, PayrollColumnCodes.CongBhDn);
                var kpcd = EffectiveValue(row, PayrollColumnCodes.Kpcd);
                return (bhxhNld, bhxhDn + kpcd);
            case PaymentObligationType.Tncn:
                return (EffectiveValue(row, PayrollColumnCodes.ThueTncn), 0m);
            default:
                return (0m, 0m);
        }
    }

    private static decimal EffectiveValue(PayrollRow row, string columnCode)
    {
        var cell = row.Cells.FirstOrDefault(c =>
            string.Equals(c.ColumnCode, columnCode, StringComparison.OrdinalIgnoreCase));
        if (cell is null) return 0m;
        return (cell.IsOverridden ? cell.OverrideValue : cell.ComputedValue) ?? 0m;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value.Substring(0, max);
}
