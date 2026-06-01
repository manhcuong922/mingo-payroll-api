namespace MPS.Infrastructure.Storage;

public sealed class MinioOptions
{
    public const string SectionName = "Minio";

    public string Endpoint { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string DefaultBucket { get; init; } = string.Empty;
    public bool UseSsl { get; init; }
    public int PresignedUrlMaxExpirySeconds { get; init; } = 900;
    public int OperationTimeoutSeconds { get; init; } = 30;
    public bool EnableBucketBootstrap { get; init; } = true;
    public bool EnableVersioning { get; init; } = true;
    public int RetryCount { get; init; } = 3;
}
