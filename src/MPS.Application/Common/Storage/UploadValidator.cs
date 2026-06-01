using FluentValidation;

namespace MPS.Application.Common.Storage;

public static class UploadValidator
{
    public static readonly IReadOnlyDictionary<string, byte[]> MagicBytes =
        new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = new byte[] { 0x25, 0x50, 0x44, 0x46 },        // %PDF
            ["image/png"] = new byte[] { 0x89, 0x50, 0x4E, 0x47 },
            ["image/jpeg"] = new byte[] { 0xFF, 0xD8, 0xFF },
            ["image/gif"] = new byte[] { 0x47, 0x49, 0x46, 0x38 },
        };

    public static async Task ValidateAsync(
        Stream content,
        string contentType,
        long maxSizeBytes,
        IReadOnlySet<string> allowedContentTypes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(allowedContentTypes);

        if (!allowedContentTypes.Contains(contentType))
            throw new ValidationException($"Content type '{contentType}' is not allowed.");

        if (content.CanSeek && content.Length > maxSizeBytes)
            throw new ValidationException(
                $"File size ({content.Length} bytes) exceeds the limit of {maxSizeBytes} bytes.");

        if (!MagicBytes.TryGetValue(contentType, out var magic))
            return;

        if (!content.CanSeek)
            throw new InvalidOperationException(
                "Stream must be seekable to perform magic-byte validation.");

        var originalPosition = content.Position;
        try
        {
            var buffer = new byte[magic.Length];
            await content.ReadExactlyAsync(buffer, cancellationToken);

            if (!buffer.AsSpan().SequenceEqual(magic))
                throw new ValidationException(
                    $"File content does not match the declared content type '{contentType}'.");
        }
        finally
        {
            content.Position = originalPosition;
        }
    }
}
