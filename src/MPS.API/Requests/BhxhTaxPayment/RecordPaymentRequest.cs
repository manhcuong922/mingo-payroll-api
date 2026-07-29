using MPS.Domain.Enums.Payroll;

namespace MPS.API.Requests.BhxhTaxPayment;

public sealed record RecordLinePaymentRequest(
    DateTime PaidDate,
    decimal PaidAmount,
    PaymentMethod PaymentMethod,
    string? TransactionRef,
    string? PaymentNotes,
    string RowVersion);

public sealed record ExcludeLineRequest(string ExcludedReason, string RowVersion);

public sealed record RevertLineRequest(string RowVersion);

public sealed record UploadEvidenceRequest(IFormFile File, string? Description);
