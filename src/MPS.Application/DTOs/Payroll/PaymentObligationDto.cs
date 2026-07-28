using MPS.Domain.Enums.Payroll;

namespace MPS.Application.DTOs.Payroll;

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
    DateTime? PaidDate,
    decimal? PaidAmount,
    PaymentMethod? PaymentMethod,
    string? TransactionRef,
    string? BankReceiptImageObjectKey,
    string? AuthorityConfirmationImageObjectKey,
    string? Notes,
    int? RecordedByUserId,
    DateTime? RecordedAt,
    int? ConfirmedByUserId,
    DateTime? ConfirmedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
