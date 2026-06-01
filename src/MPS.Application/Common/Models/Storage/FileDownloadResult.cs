namespace MPS.Application.Common.Models.Storage;

public sealed record FileDownloadResult(
    Stream Content,
    long SizeBytes,
    string ContentType,
    string? Sha256Hex);
