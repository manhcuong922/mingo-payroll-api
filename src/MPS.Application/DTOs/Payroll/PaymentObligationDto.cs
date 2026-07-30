using MPS.Domain.Enums.Payroll;

namespace MPS.Application.DTOs.Payroll;

/// <summary>
/// Chi tiết obligation cha (v3 line-level model) — không kèm lines/evidence (dùng endpoint riêng).
/// Các field aggregate (PaidAmountAggregate, ExcludedAmountAggregate, RemainingAmount, ...) compute
/// từ lines tại thời điểm query.
/// </summary>
public sealed record PaymentObligationDto(
    int Id,
    int PayrollPeriodId,
    int PeriodYear,
    int PeriodMonth,
    PaymentObligationType ObligationType,
    decimal AmountDue,
    DateTime Deadline,
    int DaysUntilDeadline,
    PaymentObligationStatus Status,
    string? Notes,
    DateTime? CompletedAt,
    int? CompletedByUserId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    decimal PaidAmountAggregate,
    decimal ExcludedAmountAggregate,
    decimal RemainingAmount,
    int TotalLinesCount,
    int PaidLinesCount,
    int ExcludedLinesCount,
    int PendingLinesCount,
    int EvidenceCount,
    bool CanBeCompleted);
