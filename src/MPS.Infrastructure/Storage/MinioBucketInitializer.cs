using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace MPS.Infrastructure.Storage;

internal sealed class MinioBucketInitializer : IHostedService
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioBucketInitializer> _logger;

    public MinioBucketInitializer(
        IMinioClient client,
        IOptions<MinioOptions> options,
        ILogger<MinioBucketInitializer> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableBucketBootstrap)
        {
            _logger.LogInformation("MinIO bucket bootstrap disabled by configuration.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.DefaultBucket))
        {
            _logger.LogWarning(
                "MinIO bootstrap skipped: Minio:DefaultBucket is not configured.");
            return;
        }

        try
        {
            var exists = await _client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_options.DefaultBucket),
                cancellationToken);

            if (!exists)
            {
                await _client.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(_options.DefaultBucket),
                    cancellationToken);
                _logger.LogInformation(
                    "Created MinIO bucket {Bucket}.", _options.DefaultBucket);
            }
            else
            {
                _logger.LogInformation(
                    "MinIO bucket {Bucket} already exists.", _options.DefaultBucket);
            }

            if (_options.EnableVersioning)
                await TryEnableVersioningAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Do not crash the app if MinIO is offline at startup; surface failures at call time.
            _logger.LogWarning(
                ex,
                "MinIO bucket bootstrap failed for bucket {Bucket}. Storage operations will throw until MinIO is reachable.",
                _options.DefaultBucket);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task TryEnableVersioningAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.SetVersioningAsync(new SetVersioningArgs()
                .WithBucket(_options.DefaultBucket)
                .WithVersioningEnabled(),
                cancellationToken);
            _logger.LogInformation(
                "Enabled versioning on bucket {Bucket}.", _options.DefaultBucket);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not enable versioning on bucket {Bucket}. Continuing without versioning.",
                _options.DefaultBucket);
        }
    }
}
