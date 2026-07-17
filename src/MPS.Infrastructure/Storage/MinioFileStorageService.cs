using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using MPS.Application.Common.Exceptions.Storage;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Storage;
using MPS.Application.Common.Storage;
using Polly;
using Polly.Retry;

namespace MPS.Infrastructure.Storage;

public sealed class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _client;
    private readonly IMinioClient _presignClient;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioFileStorageService> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public MinioFileStorageService(
        IMinioClient client,
        IOptions<MinioOptions> options,
        ILogger<MinioFileStorageService> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
        _retryPipeline = BuildRetryPipeline(_options.RetryCount, logger);
        // Chữ ký S3 V4 bao gồm Host header, nên URL presigned phải được KÝ với đúng
        // host mà browser sẽ gọi — thay host bằng string-replace sau khi ký sẽ làm
        // MinIO trả SignatureDoesNotMatch.
        _presignClient = string.IsNullOrWhiteSpace(_options.ExternalEndpoint)
            ? client
            : BuildExternalPresignClient(_options);
    }

    private static IMinioClient BuildExternalPresignClient(MinioOptions options)
    {
        var client = new MinioClient()
            .WithEndpoint(options.ExternalEndpoint)
            .WithCredentials(options.AccessKey, options.SecretKey)
            // Region cố định để tránh lookup bucket-location qua mạng —
            // ExternalEndpoint chỉ reachable từ browser, không phải từ container API.
            .WithRegion("us-east-1");
        if (options.ExternalUseSsl)
            client = client.WithSSL();
        return client.Build();
    }

    public async Task<StoredFileMetadata> UploadAsync(
        FileUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectKeyBuilder.EnsureValid(request.ObjectKey);

        var bucket = ResolveBucket(request.Bucket);

        // Buffer content so hash can be computed after the full stream is read.
        // CryptoStream in Read mode does not call FlushFinalBlock on Dispose,
        // so the CryptoStream approach would crash when accessing sha256.Hash.
        using var buffer = new MemoryStream();
        await request.Content.CopyToAsync(buffer, cancellationToken);

        var hashHex = Convert.ToHexString(
            SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length))
        ).ToLowerInvariant();

        buffer.Position = 0;

        var args = new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(request.ObjectKey)
            .WithStreamData(buffer)
            .WithObjectSize(buffer.Length)
            .WithContentType(request.ContentType);

        if (request.Metadata is { Count: > 0 })
            args = args.WithHeaders(new Dictionary<string, string>(request.Metadata));

        try
        {
            await _client.PutObjectAsync(args, cancellationToken);
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            throw new FileStorageUnavailableException(
                $"MinIO upload failed for '{request.ObjectKey}'.", ex);
        }
        catch (MinioException ex)
        {
            throw new FileStorageException(
                $"MinIO upload failed for '{request.ObjectKey}'.", ex);
        }

        var stat = await StatAsync(bucket, request.ObjectKey, cancellationToken);

        _logger.LogInformation(
            "Uploaded {ObjectKey} to bucket {Bucket} ({Size} bytes, sha256={Sha256}).",
            request.ObjectKey, bucket, stat.Size, hashHex);

        return new StoredFileMetadata(
            Bucket: bucket,
            ObjectKey: request.ObjectKey,
            SizeBytes: stat.Size,
            Sha256Hex: hashHex,
            ContentType: request.ContentType,
            UploadedAtUtc: DateTime.UtcNow,
            ETag: stat.ETag);
    }

    public async Task<FileDownloadResult> DownloadAsync(
        string objectKey,
        string? bucket = null,
        CancellationToken cancellationToken = default)
    {
        ObjectKeyBuilder.EnsureValid(objectKey);
        var resolvedBucket = ResolveBucket(bucket);

        var memoryStream = new MemoryStream();
        ObjectStatModel? stat = null;

        try
        {
            await _retryPipeline.ExecuteAsync(async ct =>
            {
                memoryStream.SetLength(0);
                memoryStream.Position = 0;

                var args = new GetObjectArgs()
                    .WithBucket(resolvedBucket)
                    .WithObject(objectKey)
                    .WithCallbackStream(async (s, innerCt) =>
                    {
                        await s.CopyToAsync(memoryStream, innerCt);
                    });

                var stream = await _client.GetObjectAsync(args, ct);
                stat = new ObjectStatModel(stream.Size, stream.ETag, stream.ContentType);
            }, cancellationToken);
        }
        catch (ObjectNotFoundException)
        {
            await memoryStream.DisposeAsync();
            throw new FileNotFoundInStorageException(resolvedBucket, objectKey);
        }
        catch (BucketNotFoundException ex)
        {
            await memoryStream.DisposeAsync();
            throw new FileNotFoundInStorageException(resolvedBucket, objectKey)
            {
                Source = ex.Source,
            };
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            await memoryStream.DisposeAsync();
            throw new FileStorageUnavailableException(
                $"MinIO download failed for '{objectKey}'.", ex);
        }
        catch (MinioException ex)
        {
            await memoryStream.DisposeAsync();
            throw new FileStorageException(
                $"MinIO download failed for '{objectKey}'.", ex);
        }

        memoryStream.Position = 0;

        return new FileDownloadResult(
            Content: memoryStream,
            SizeBytes: stat?.Size ?? memoryStream.Length,
            ContentType: stat?.ContentType ?? "application/octet-stream",
            Sha256Hex: null);
    }

    public async Task<string> GeneratePresignedDownloadUrlAsync(
        string objectKey,
        TimeSpan expiry,
        string? bucket = null,
        CancellationToken cancellationToken = default)
    {
        ObjectKeyBuilder.EnsureValid(objectKey);
        EnsureExpiryWithinLimit(expiry);

        var resolvedBucket = ResolveBucket(bucket);

        try
        {
            var url = await _presignClient.PresignedGetObjectAsync(new PresignedGetObjectArgs()
                .WithBucket(resolvedBucket)
                .WithObject(objectKey)
                .WithExpiry((int)expiry.TotalSeconds));

            return url;
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            throw new FileStorageUnavailableException(
                $"Failed to generate presigned URL for '{objectKey}'.", ex);
        }
        catch (MinioException ex)
        {
            throw new FileStorageException(
                $"Failed to generate presigned URL for '{objectKey}'.", ex);
        }
    }

    public async Task<PresignedUploadResult> GeneratePresignedUploadUrlAsync(
        string objectKey,
        TimeSpan expiry,
        string? bucket = null,
        CancellationToken cancellationToken = default)
    {
        ObjectKeyBuilder.EnsureValid(objectKey);
        EnsureExpiryWithinLimit(expiry);

        var resolvedBucket = ResolveBucket(bucket);

        try
        {
            var url = await _presignClient.PresignedPutObjectAsync(new PresignedPutObjectArgs()
                .WithBucket(resolvedBucket)
                .WithObject(objectKey)
                .WithExpiry((int)expiry.TotalSeconds));

            return new PresignedUploadResult(
                Url: url,
                ObjectKey: objectKey,
                Bucket: resolvedBucket,
                ExpiresAtUtc: DateTime.UtcNow.Add(expiry),
                RequiredHeaders: null);
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            throw new FileStorageUnavailableException(
                $"Failed to generate presigned upload URL for '{objectKey}'.", ex);
        }
        catch (MinioException ex)
        {
            throw new FileStorageException(
                $"Failed to generate presigned upload URL for '{objectKey}'.", ex);
        }
    }

    public async Task DeleteAsync(
        string objectKey,
        string? bucket = null,
        CancellationToken cancellationToken = default)
    {
        ObjectKeyBuilder.EnsureValid(objectKey);
        var resolvedBucket = ResolveBucket(bucket);

        try
        {
            await _retryPipeline.ExecuteAsync(async ct =>
            {
                await _client.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(resolvedBucket)
                    .WithObject(objectKey), ct);
            }, cancellationToken);

            _logger.LogInformation(
                "Deleted {ObjectKey} from bucket {Bucket}.", objectKey, resolvedBucket);
        }
        catch (ObjectNotFoundException)
        {
            // Idempotent delete: object already absent is success.
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            throw new FileStorageUnavailableException(
                $"MinIO delete failed for '{objectKey}'.", ex);
        }
        catch (MinioException ex)
        {
            throw new FileStorageException(
                $"MinIO delete failed for '{objectKey}'.", ex);
        }
    }

    public async Task<bool> ExistsAsync(
        string objectKey,
        string? bucket = null,
        CancellationToken cancellationToken = default)
    {
        ObjectKeyBuilder.EnsureValid(objectKey);
        var resolvedBucket = ResolveBucket(bucket);

        try
        {
            await StatAsync(resolvedBucket, objectKey, cancellationToken);
            return true;
        }
        catch (FileNotFoundInStorageException)
        {
            return false;
        }
    }

    private async Task<ObjectStatModel> StatAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var stat = await _retryPipeline.ExecuteAsync(async ct =>
                await _client.StatObjectAsync(new StatObjectArgs()
                    .WithBucket(bucket)
                    .WithObject(objectKey), ct), cancellationToken);

            return new ObjectStatModel(stat.Size, stat.ETag, stat.ContentType);
        }
        catch (ObjectNotFoundException)
        {
            throw new FileNotFoundInStorageException(bucket, objectKey);
        }
        catch (BucketNotFoundException)
        {
            throw new FileNotFoundInStorageException(bucket, objectKey);
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            throw new FileStorageUnavailableException(
                $"MinIO stat failed for '{objectKey}'.", ex);
        }
        catch (MinioException ex)
        {
            throw new FileStorageException(
                $"MinIO stat failed for '{objectKey}'.", ex);
        }
    }

    private string ResolveBucket(string? requested)
    {
        var bucket = string.IsNullOrWhiteSpace(requested) ? _options.DefaultBucket : requested;
        if (string.IsNullOrWhiteSpace(bucket))
            throw new InvalidOperationException(
                "No bucket specified and Minio:DefaultBucket is not configured.");
        return bucket;
    }

    private void EnsureExpiryWithinLimit(TimeSpan expiry)
    {
        if (expiry <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(expiry), expiry, "Expiry must be positive.");

        if (expiry.TotalSeconds > _options.PresignedUrlMaxExpirySeconds)
            throw new ArgumentOutOfRangeException(
                nameof(expiry), expiry,
                $"Expiry exceeds configured limit of {_options.PresignedUrlMaxExpirySeconds} seconds.");
    }

    private static ResiliencePipeline BuildRetryPipeline(int retryCount, ILogger logger)
    {
        if (retryCount <= 0)
            return ResiliencePipeline.Empty;

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = retryCount,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(200),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransient),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Transient MinIO failure (attempt {Attempt}). Retrying after {Delay}ms.",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException
            or TimeoutException
            or TaskCanceledException
            or System.Net.Sockets.SocketException
            or System.IO.IOException;

    private sealed record ObjectStatModel(long Size, string ETag, string ContentType);
}
