namespace MPS.Application.Common.Models.Storage;

public sealed record FileUploadRequest(
    Stream Content,
    string ObjectKey,
    string ContentType,
    long? KnownSizeBytes = null,
    string? Bucket = null,
    IReadOnlyDictionary<string, string>? Metadata = null);
