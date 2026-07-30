using MPS.Domain.Enums.Payroll;

namespace MPS.Application.DTOs.Payroll;

public sealed record PaymentObligationLineDto(
    int Id,
    int PaymentObligationId,
    int EmployeeInformationId,
    string EmployeeCode,
    string EmployeeFullName,
    decimal EmployeeContribution,
    decimal EmployerContribution,
    decimal TotalAmount,
    PaymentLineStatus LineStatus,
    DateTime? PaidDate,
    decimal? PaidAmount,
    PaymentMethod? PaymentMethod,
    string? TransactionRef,
    string? PaymentNotes,
    int? RecordedByUserId,
    string? RecordedByUserName,
    DateTime? RecordedAt,
    string? ExcludedReason,
    int? ExcludedByUserId,
    string? ExcludedByUserName,
    DateTime? ExcludedAt,
    string RowVersion);
