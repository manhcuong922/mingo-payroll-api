using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace MPS.Infrastructure.Storage;

public sealed class MinioHealthCheck : IHealthCheck
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;

    public MinioHealthCheck(IMinioClient client, IOptions<MinioOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.DefaultBucket))
            return HealthCheckResult.Degraded("Minio:DefaultBucket is not configured.");

        try
        {
            var exists = await _client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_options.DefaultBucket),
                cancellationToken);

            return exists
                ? HealthCheckResult.Healthy(
                    $"MinIO reachable, bucket '{_options.DefaultBucket}' exists.")
                : HealthCheckResult.Degraded(
                    $"MinIO reachable but bucket '{_options.DefaultBucket}' is missing.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"MinIO unreachable at '{_options.Endpoint}'.", ex);
        }
    }
}
