using MPS.Domain.Enums.Payroll;

namespace MPS.API.Requests.BhxhTaxPayment;

public sealed record RecordPaymentRequest(
    DateTime PaidDate,
    decimal PaidAmount,
    PaymentMethod PaymentMethod,
    string? TransactionRef,
    string? Notes);

public sealed record ConfirmDiscrepancyRequest(string? ConfirmationNote);

public sealed record UploadEvidenceRequest(IFormFile File);
