namespace MPS.Application.Common.Models.Storage;

public sealed record StoredFileMetadata(
    string Bucket,
    string ObjectKey,
    long SizeBytes,
    string Sha256Hex,
    string ContentType,
    DateTime UploadedAtUtc,
    string? ETag);
