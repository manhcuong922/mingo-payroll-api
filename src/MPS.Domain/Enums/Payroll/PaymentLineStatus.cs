namespace MPS.Domain.Enums.Payroll;

/// <summary>
/// Trạng thái 1 line (nghĩa vụ nộp cho 1 NV) trong <c>PaymentObligation</c>.
/// <list type="bullet">
///   <item><see cref="Pending"/>: chưa xử lý.</item>
///   <item><see cref="Paid"/>: đã ghi nhận nộp (có PaidDate/PaidAmount/Method).</item>
///   <item><see cref="Excluded"/>: bỏ qua khỏi lô nộp (kèm ExcludedReason bắt buộc).</item>
/// </list>
/// </summary>
public enum PaymentLineStatus
{
    Pending = 1,
    Paid = 2,
    Excluded = 3,
}
