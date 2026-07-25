namespace MPS.Domain.Enums.Payroll;

/// <summary>
/// Vòng đời 1 nghĩa vụ nộp BHXH/TNCN.
/// <list type="bullet">
///   <item><see cref="Pending"/>: mới sinh khi kỳ Locked, chưa ghi nhận nộp.</item>
///   <item><see cref="PaidConfirmed"/>: đã ghi nhận nộp, số tiền khớp AmountDue.</item>
///   <item><see cref="PaidWithDiscrepancy"/>: đã ghi nhận nhưng số thực nộp ≠ AmountDue, chờ user xác nhận.</item>
///   <item><see cref="Overdue"/>: quá deadline nhưng vẫn Pending — job daily flip cờ này.</item>
/// </list>
/// </summary>
public enum PaymentObligationStatus
{
    Pending = 1,
    PaidConfirmed = 2,
    PaidWithDiscrepancy = 3,
    Overdue = 4,
}
