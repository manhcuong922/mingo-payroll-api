namespace MPS.Application.Common.Models.Storage;

public sealed record PresignedUploadResult(
    string Url,
    string ObjectKey,
    string Bucket,
    DateTime ExpiresAtUtc,
    IReadOnlyDictionary<string, string>? RequiredHeaders = null);
