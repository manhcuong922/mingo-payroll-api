using MPS.Domain.Entities.Payroll.BhxhTax;
using MPS.Domain.Enums.Payroll;

namespace MPS.Domain.Interfaces.Payroll;

public interface IPaymentObligationRepository
{
    // ===== Obligation (cha) =====

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

    /// <summary>Job daily: nghĩa vụ chưa Completed có deadline &lt; ngày mốc → Overdue.</summary>
    Task<IReadOnlyList<PaymentObligation>> ListPendingOrPartiallyPaidWithDeadlineBeforeAsync(
        DateTime deadlineExclusiveUpperBound,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<PaymentObligation> obligations, CancellationToken cancellationToken = default);

    // ===== Line =====

    Task<PaymentObligationLine?> GetLineByIdAsync(
        int lineId, bool asNoTracking, CancellationToken cancellationToken = default);

    Task AddLinesAsync(
        IEnumerable<PaymentObligationLine> lines, CancellationToken cancellationToken = default);

    Task DeleteLinesByObligationAsync(int obligationId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<PaymentObligationLine> Items, int TotalCount)> GetLinesByObligationAsync(
        int obligationId,
        PaymentLineStatus? statusFilter,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Load toàn bộ line của obligation — dùng cho export Excel.</summary>
    Task<IReadOnlyList<PaymentObligationLine>> GetAllLinesByObligationAsync(
        int obligationId, CancellationToken cancellationToken = default);

    /// <summary>Đếm line theo status — dùng cho derive parent status + guard Complete.</summary>
    Task<PaymentObligationLineCounts> CountLinesByStatusAsync(
        int obligationId, CancellationToken cancellationToken = default);

    /// <summary>Sum aggregate cho line — dùng cho DTO summary.</summary>
    Task<PaymentObligationLineAggregates> GetLineAggregatesAsync(
        int obligationId, CancellationToken cancellationToken = default);

    // ===== Evidence =====

    Task AddEvidenceAsync(PaymentObligationEvidence evidence, CancellationToken cancellationToken = default);

    Task<PaymentObligationEvidence?> GetEvidenceByIdAsync(
        int evidenceId, bool asNoTracking, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentObligationEvidence>> ListEvidenceByObligationAsync(
        int obligationId, CancellationToken cancellationToken = default);

    Task<int> CountEvidenceAsync(int obligationId, CancellationToken cancellationToken = default);

    void RemoveEvidence(PaymentObligationEvidence evidence);

    // ===== Common =====

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record PaymentObligationLineCounts(int Pending, int Paid, int Excluded)
{
    public int Total => Pending + Paid + Excluded;
    public bool AllHandled => Pending == 0 && Total > 0;
}

public sealed record PaymentObligationLineAggregates(
    decimal PaidAmount,
    decimal ExcludedAmount);
