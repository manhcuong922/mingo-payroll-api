using MPS.Application.DTOs.Payroll;
using MPS.Domain.Entities.Payroll.BhxhTax;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Common;

/// <summary>
/// Map entity → DTO + tính <c>DaysUntilDeadline</c> tương đối theo ngày hiện tại (âm khi quá hạn).
/// Cần lấy Year/Month từ nav PayrollPeriod — caller phải Include khi load.
/// </summary>
internal static class PaymentObligationMapper
{
    public static PaymentObligationDto Map(PaymentObligation o, DateTime today)
    {
        var days = (int)(o.Deadline.Date - today.Date).TotalDays;
        return new PaymentObligationDto(
            Id: o.Id,
            PayrollPeriodId: o.PayrollPeriodId,
            PeriodYear: o.PayrollPeriod?.PeriodYear ?? 0,
            PeriodMonth: o.PayrollPeriod?.PeriodMonth ?? 0,
            ObligationType: o.ObligationType,
            AmountDue: o.AmountDue,
            Deadline: o.Deadline,
            DaysUntilDeadline: days,
            Status: o.Status,
            PaidDate: o.PaidDate,
            PaidAmount: o.PaidAmount,
            PaymentMethod: o.PaymentMethod,
            TransactionRef: o.TransactionRef,
            BankReceiptImageObjectKey: o.BankReceiptImageObjectKey,
            AuthorityConfirmationImageObjectKey: o.AuthorityConfirmationImageObjectKey,
            Notes: o.Notes,
            RecordedByUserId: o.RecordedByUserId,
            RecordedAt: o.RecordedAt,
            ConfirmedByUserId: o.ConfirmedByUserId,
            ConfirmedAt: o.ConfirmedAt,
            CreatedAt: o.CreatedAt,
            UpdatedAt: o.UpdatedAt);
    }
}
