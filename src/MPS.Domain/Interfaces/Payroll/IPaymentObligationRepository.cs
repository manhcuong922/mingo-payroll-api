using MPS.Domain.Entities.Payroll.BhxhTax;
using MPS.Domain.Enums.Payroll;

namespace MPS.Domain.Interfaces.Payroll;

public interface IPaymentObligationRepository
{
    Task<PaymentObligation?> GetByIdAsync(int id, bool asNoTracking, CancellationToken cancellationToken = default);

    /// <summary>Lấy 2 nghĩa vụ (Bhxh + Tncn) của 1 kỳ — dùng để check idempotent + list detail.</summary>
    Task<IReadOnlyList<PaymentObligation>> GetByPeriodAsync(int periodId, CancellationToken cancellationToken = default);

    /// <summary>Query có filter — dùng cho endpoint list.</summary>
    Task<IReadOnlyList<PaymentObligation>> QueryAsync(
        int? year,
        int? month,
        PaymentObligationType? type,
        PaymentObligationStatus? status,
        CancellationToken cancellationToken = default);

    /// <summary>Job daily: liệt kê nghĩa vụ Pending có deadline &lt; ngày mốc để chuyển sang Overdue.</summary>
    Task<IReadOnlyList<PaymentObligation>> ListPendingWithDeadlineBeforeAsync(
        DateTime deadlineExclusiveUpperBound,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<PaymentObligation> obligations, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
