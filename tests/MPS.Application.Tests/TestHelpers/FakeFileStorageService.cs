using System.Security.Cryptography;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Storage;

namespace MPS.Application.Tests.TestHelpers;

/// <summary>
/// In-memory <see cref="IFileStorageService"/> for integration tests. Stores uploaded
/// blobs in a dictionary keyed by bucket/objectKey, computes SHA-256 like the real impl.
/// </summary>
internal sealed class FakeFileStorageService : IFileStorageService
{
    public const string DefaultBucket = "test-bucket";

    private readonly Dictionary<string, byte[]> _store = new();

    public IReadOnlyDictionary<string, byte[]> Uploaded => _store;

    public async Task<StoredFileMetadata> UploadAsync(FileUploadRequest request, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await request.Content.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();

        var bucket = request.Bucket ?? DefaultBucket;
        var key = $"{bucket}/{request.ObjectKey}";
        _store[key] = bytes;

        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new StoredFileMetadata(
            Bucket: bucket,
            ObjectKey: request.ObjectKey,
            SizeBytes: bytes.LongLength,
            Sha256Hex: sha,
            ContentType: request.ContentType,
            UploadedAtUtc: DateTime.UtcNow,
            ETag: "\"fake-etag\"");
    }

    public Task<FileDownloadResult> DownloadAsync(string objectKey, string? bucket = null, CancellationToken cancellationToken = default)
    {
        var key = $"{bucket ?? DefaultBucket}/{objectKey}";
        if (!_store.TryGetValue(key, out var bytes))
            throw new InvalidOperationException($"Object not found: {key}");
        return Task.FromResult(new FileDownloadResult(
            Content: new MemoryStream(bytes),
            SizeBytes: bytes.LongLength,
            ContentType: "application/pdf",
            Sha256Hex: null));
    }

    public Task<string> GeneratePresignedDownloadUrlAsync(string objectKey, TimeSpan expiry, string? bucket = null, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://fake.minio/{bucket ?? DefaultBucket}/{objectKey}?expires={expiry.TotalSeconds}");

    public Task<PresignedUploadResult> GeneratePresignedUploadUrlAsync(string objectKey, TimeSpan expiry, string? bucket = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PresignedUploadResult(
            Url: $"https://fake.minio/{bucket ?? DefaultBucket}/{objectKey}?expires={expiry.TotalSeconds}",
            ObjectKey: objectKey,
            Bucket: bucket ?? DefaultBucket,
            ExpiresAtUtc: DateTime.UtcNow.Add(expiry),
            RequiredHeaders: null));

    public Task DeleteAsync(string objectKey, string? bucket = null, CancellationToken cancellationToken = default)
    {
        _store.Remove($"{bucket ?? DefaultBucket}/{objectKey}");
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string objectKey, string? bucket = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.ContainsKey($"{bucket ?? DefaultBucket}/{objectKey}"));
}
