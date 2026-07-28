using MediatR;

namespace MPS.Application.Common.Models.Payroll;

/// <summary>
/// Publish sau khi <c>LockPayrollPeriodCommandHandler</c> chuyển kỳ Finalized → Locked thành công.
/// Handler <c>CreatePaymentObligationsOnPeriodLockedHandler</c> tiêu thụ event này để sinh 2 nghĩa vụ
/// (BHXH + TNCN) vào bảng <c>PaymentObligations</c>. Idempotent — re-lock cùng kỳ không tạo trùng.
/// </summary>
public sealed record PayrollPeriodLockedNotification(
    int PeriodId,
    int PeriodYear,
    int PeriodMonth,
    int LockedByUserId,
    DateTime LockedAtUtc) : INotification;
