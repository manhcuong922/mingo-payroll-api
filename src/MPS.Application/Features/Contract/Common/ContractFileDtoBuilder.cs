using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Features.Contract.Common;

/// <summary>
/// Build <see cref="ContractFileDto"/> từ entity + gen presigned download URL.
/// Tách helper để 3 handler (Contract / Termination / Appendix files) dùng chung.
/// </summary>
internal static class ContractFileDtoBuilder
{
    /// <summary>Default TTL cho presigned URL — 15 phút (plan §11.3 security checklist).</summary>
    public static readonly TimeSpan DefaultPresignedTtl = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Build DTOs theo thứ tự input (assumption: input đã sort sẵn theo Version DESC).
    /// Gen presigned URL tuần tự để hash-by-hash exception nếu MinIO down.
    /// </summary>
    public static async Task<IReadOnlyList<ContractFileDto>> BuildAsync(
        IReadOnlyList<ContractFile> files,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0) return Array.Empty<ContractFileDto>();

        var result = new List<ContractFileDto>(files.Count);
        foreach (var file in files)
        {
            var url = await storage.GeneratePresignedDownloadUrlAsync(
                file.MinioObjectKey,
                DefaultPresignedTtl,
                file.MinioBucket,
                cancellationToken);

            result.Add(new ContractFileDto(
                Id: file.Id,
                TargetType: file.TargetType.ToString(),
                TargetId: file.TargetId,
                FileName: file.FileName,
                MimeType: file.MimeType,
                FileSizeBytes: file.FileSizeBytes,
                Sha256: file.Sha256,
                Version: file.Version,
                IsCurrent: file.IsCurrent,
                SignedBySource: file.SignedBySource,
                UploadedAt: file.UploadedAt,
                UploadedByUserId: file.UploadedByUserId,
                PresignedDownloadUrl: url));
        }
        return result;
    }
}
