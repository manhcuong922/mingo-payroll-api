namespace MPS.Domain.Enums.Payroll;

/// <summary>
/// Vòng đời 1 nghĩa vụ nộp BHXH/TNCN (v3 line-level model).
/// <list type="bullet">
///   <item><see cref="Pending"/>: mới sinh khi kỳ Locked, chưa có line nào Paid/Excluded.</item>
///   <item><see cref="PartiallyPaid"/>: có ≥1 line Paid/Excluded, còn line Pending — hoặc mọi line đã xử lý nhưng user chưa bấm Hoàn thành.</item>
///   <item><see cref="Completed"/>: user đã bấm Hoàn thành (mọi line ∈ {Paid, Excluded} + có ≥1 evidence). Khoá mọi mutation.</item>
///   <item><see cref="Overdue"/>: quá deadline mà chưa Completed — job daily flip cờ này từ Pending/PartiallyPaid.</item>
/// </list>
/// </summary>
public enum PaymentObligationStatus
{
    Pending = 1,
    PartiallyPaid = 2,
    Completed = 3,
    Overdue = 4,
}
