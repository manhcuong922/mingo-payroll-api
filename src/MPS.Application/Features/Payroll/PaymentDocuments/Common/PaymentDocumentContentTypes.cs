namespace MPS.Application.Features.Payroll.PaymentDocuments.Common;

/// <summary>MIME type của các chứng từ xuất ra — gom một chỗ để 3 handler không gõ lệch nhau.</summary>
public static class PaymentDocumentContentTypes
{
    public const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string Zip = "application/zip";
}
