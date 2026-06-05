using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Contract.Common;

/// <summary>
/// Lightweight checks for uploaded PDF blobs.
/// - Size cap (default 20 MB) to keep activation lightweight; large PDFs of HĐ should be
///   rare. Plan §7.5.
/// - Magic-bytes prefix <c>%PDF-</c> to catch trivially mis-labeled uploads
///   (e.g. .docx with renamed extension). Does NOT validate signature, structure,
///   or page count — that's the signing module's job.
/// </summary>
public static class PdfStreamValidator
{
    public const long DefaultMaxSizeBytes = 20L * 1024 * 1024; // 20 MB
    public const string PdfContentType = "application/pdf";

    private static readonly byte[] PdfMagicBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D }; // %PDF-

    /// <summary>
    /// Throws <see cref="DomainValidationException"/> when the upload fails any check.
    /// Reads the first 5 bytes and rewinds, so the caller can re-use the stream.
    /// </summary>
    public static async Task ValidateAsync(
        Stream content,
        long fileSizeBytes,
        string contentType,
        long maxSizeBytes = DefaultMaxSizeBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (fileSizeBytes <= 0)
            throw new DomainValidationException("Uploaded file is empty.");
        if (fileSizeBytes > maxSizeBytes)
            throw new DomainValidationException(
                $"Uploaded file is {fileSizeBytes / 1024 / 1024} MB; the limit is {maxSizeBytes / 1024 / 1024} MB.");

        if (!string.Equals(contentType, PdfContentType, StringComparison.OrdinalIgnoreCase))
            throw new DomainValidationException(
                $"Unsupported content type '{contentType}'. Expected '{PdfContentType}'.");

        if (!content.CanSeek)
            throw new DomainValidationException(
                "Uploaded stream must be seekable to verify PDF magic bytes.");

        var startPosition = content.Position;
        var header = new byte[PdfMagicBytes.Length];
        var read = await content.ReadAsync(header.AsMemory(), cancellationToken);

        if (read < PdfMagicBytes.Length || !header.SequenceEqual(PdfMagicBytes))
            throw new DomainValidationException(
                "File does not start with the PDF magic bytes ('%PDF-').");

        content.Position = startPosition;
    }
}
