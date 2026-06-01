using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Moq;
using MPS.Application.Common.Exceptions.Storage;
using MPS.Application.Common.Models.Storage;
using MPS.Infrastructure.Storage;

namespace MPS.Infrastructure.Tests.Storage;

public sealed class MinioFileStorageServiceTests
{
    private static MinioOptions DefaultOptions(string defaultBucket = "test-bucket") => new()
    {
        Endpoint = "localhost:9000",
        AccessKey = "test",
        SecretKey = "test-secret",
        DefaultBucket = defaultBucket,
        UseSsl = false,
        PresignedUrlMaxExpirySeconds = 900,
        OperationTimeoutSeconds = 30,
        EnableBucketBootstrap = false,
        EnableVersioning = false,
        RetryCount = 0,
    };

    private static MinioFileStorageService BuildSut(
        Mock<IMinioClient>? clientMock = null,
        MinioOptions? options = null)
    {
        var client = (clientMock ?? new Mock<IMinioClient>(MockBehavior.Loose)).Object;
        return new MinioFileStorageService(
            client,
            Options.Create(options ?? DefaultOptions()),
            NullLogger<MinioFileStorageService>.Instance);
    }

    private static FileUploadRequest UploadRequest(string objectKey = "files/a.pdf") =>
        new(
            Content: new MemoryStream(new byte[] { 1, 2, 3 }),
            ObjectKey: objectKey,
            ContentType: "application/pdf");

    [Fact]
    public async Task UploadAsync_Throws_OnInvalidKey()
    {
        var sut = BuildSut();

        var act = async () => await sut.UploadAsync(UploadRequest("../escape"));

        await act.Should().ThrowAsync<InvalidObjectKeyException>();
    }

    [Fact]
    public async Task UploadAsync_Throws_OnEmptyKey()
    {
        var sut = BuildSut();

        var act = async () => await sut.UploadAsync(UploadRequest(string.Empty));

        await act.Should().ThrowAsync<InvalidObjectKeyException>();
    }

    [Fact]
    public async Task DownloadAsync_Throws_OnInvalidKey()
    {
        var sut = BuildSut();

        var act = async () => await sut.DownloadAsync("\\bad");

        await act.Should().ThrowAsync<InvalidObjectKeyException>();
    }

    [Fact]
    public async Task DeleteAsync_Throws_OnInvalidKey()
    {
        var sut = BuildSut();

        var act = async () => await sut.DeleteAsync("a\0b");

        await act.Should().ThrowAsync<InvalidObjectKeyException>();
    }

    [Fact]
    public async Task ExistsAsync_Throws_OnInvalidKey()
    {
        var sut = BuildSut();

        var act = async () => await sut.ExistsAsync("../traversal");

        await act.Should().ThrowAsync<InvalidObjectKeyException>();
    }

    [Fact]
    public async Task GeneratePresignedDownloadUrlAsync_Throws_OnInvalidKey()
    {
        var sut = BuildSut();

        var act = async () =>
            await sut.GeneratePresignedDownloadUrlAsync("/abs", TimeSpan.FromMinutes(5));

        await act.Should().ThrowAsync<InvalidObjectKeyException>();
    }

    [Fact]
    public async Task GeneratePresignedDownloadUrlAsync_Throws_WhenExpiryNonPositive()
    {
        var sut = BuildSut();

        var act = async () =>
            await sut.GeneratePresignedDownloadUrlAsync("good/key.pdf", TimeSpan.Zero);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GeneratePresignedDownloadUrlAsync_Throws_WhenExpiryExceedsLimit()
    {
        var options = DefaultOptions();
        var sut = BuildSut(options: options);

        var act = async () =>
            await sut.GeneratePresignedDownloadUrlAsync(
                "good/key.pdf",
                TimeSpan.FromSeconds(options.PresignedUrlMaxExpirySeconds + 1));

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GeneratePresignedUploadUrlAsync_Throws_WhenExpiryExceedsLimit()
    {
        var sut = BuildSut();

        var act = async () =>
            await sut.GeneratePresignedUploadUrlAsync(
                "good/key.pdf",
                TimeSpan.FromHours(24));

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task UploadAsync_Throws_WhenBucketUnspecified_AndDefaultEmpty()
    {
        var options = DefaultOptions(defaultBucket: string.Empty);
        var sut = BuildSut(options: options);

        var act = async () => await sut.UploadAsync(UploadRequest());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*DefaultBucket*");
    }

    [Fact]
    public async Task DeleteAsync_CallsClient_WithResolvedDefaultBucket()
    {
        var clientMock = new Mock<IMinioClient>();
        clientMock
            .Setup(c => c.RemoveObjectAsync(
                It.IsAny<RemoveObjectArgs>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = BuildSut(clientMock);

        await sut.DeleteAsync("contracts/2026/06/HD-1/v1.pdf");

        clientMock.Verify(c => c.RemoveObjectAsync(
            It.IsAny<RemoveObjectArgs>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_TreatsMissingObject_AsSuccess()
    {
        var clientMock = new Mock<IMinioClient>();
        clientMock
            .Setup(c => c.RemoveObjectAsync(
                It.IsAny<RemoveObjectArgs>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Minio.Exceptions.ObjectNotFoundException("missing", "not found"));

        var sut = BuildSut(clientMock);

        var act = async () => await sut.DeleteAsync("contracts/2026/06/HD-1/v1.pdf");

        await act.Should().NotThrowAsync();
    }
}
