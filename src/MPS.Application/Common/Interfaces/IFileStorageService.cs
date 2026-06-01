using MPS.Application.Common.Models.Storage;

namespace MPS.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<StoredFileMetadata> UploadAsync(
        FileUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<FileDownloadResult> DownloadAsync(
        string objectKey,
        string? bucket = null,
        CancellationToken cancellationToken = default);

    Task<string> GeneratePresignedDownloadUrlAsync(
        string objectKey,
        TimeSpan expiry,
        string? bucket = null,
        CancellationToken cancellationToken = default);

    Task<PresignedUploadResult> GeneratePresignedUploadUrlAsync(
        string objectKey,
        TimeSpan expiry,
        string? bucket = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string objectKey,
        string? bucket = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string objectKey,
        string? bucket = null,
        CancellationToken cancellationToken = default);
}
