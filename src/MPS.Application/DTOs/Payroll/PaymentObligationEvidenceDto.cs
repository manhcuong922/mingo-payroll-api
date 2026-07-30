namespace MPS.Application.DTOs.Payroll;

public sealed record PaymentObligationEvidenceDto(
    int Id,
    int PaymentObligationId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string? Description,
    int UploadedByUserId,
    string? UploadedByUserName,
    DateTime UploadedAt,
    string? PresignedDownloadUrl);
