using MPS.Domain.Exceptions;

namespace MPS.Application.Features.CompanyPolicy.Commands;

/// <summary>
/// Validate file upload nội quy: hỗ trợ PDF + DOCX, max 25 MB.
/// </summary>
internal static class CompanyPolicyFileValidator
{
    public const long MaxFileSizeBytes = 25L * 1024L * 1024L;

    public const string PdfMimeType = "application/pdf";
    public const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static readonly byte[] PdfMagic  = [0x25, 0x50, 0x44, 0x46]; // %PDF
    private static readonly byte[] DocxMagic = [0x50, 0x4B, 0x03, 0x04]; // PK zip header

    public sealed record DetectedKind(string Extension, string MimeType);

    public static async Task<DetectedKind> ValidateAsync(
        Stream stream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken)
    {
        if (fileSizeBytes <= 0)
            throw new DomainValidationException("COMPANY_POLICY_FILE_EMPTY: File rỗng.");
        if (fileSizeBytes > MaxFileSizeBytes)
            throw new DomainValidationException(
                $"COMPANY_POLICY_FILE_TOO_LARGE: Tối đa {MaxFileSizeBytes / (1024 * 1024)} MB.");

        var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        if (ext != "pdf" && ext != "docx")
            throw new DomainValidationException(
                "COMPANY_POLICY_FILE_TYPE_NOT_SUPPORTED: Chỉ chấp nhận file .pdf hoặc .docx.");

        var header = new byte[4];
        var read = await stream.ReadAsync(header, cancellationToken);
        stream.Seek(0, SeekOrigin.Begin);
        if (read < 4)
            throw new DomainValidationException("COMPANY_POLICY_FILE_INVALID: File không đọc được header.");

        if (ext == "pdf")
        {
            if (!header.SequenceEqual(PdfMagic))
                throw new DomainValidationException("INVALID_PDF: File không phải PDF hợp lệ.");
            return new DetectedKind("pdf", PdfMimeType);
        }

        // ext == "docx"
        if (!header.SequenceEqual(DocxMagic))
            throw new DomainValidationException("INVALID_DOCX: File không phải DOCX (ZIP) hợp lệ.");
        return new DetectedKind("docx", DocxMimeType);
    }
}
