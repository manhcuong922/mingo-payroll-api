using MPS.Application.Common.Models.Storage;
using MPS.Application.DTOs;
using MPS.Domain.Enums;

namespace MPS.Application.Services;

public interface IProductionAttachmentService
{
    Task<ProductionAttachmentDto> UploadAsync(
        AttachmentOwnerType ownerType, int ownerId,
        Stream content, string fileName, string contentType, string? note,
        CancellationToken ct);

    Task<IReadOnlyList<ProductionAttachmentDto>> ListAsync(
        AttachmentOwnerType ownerType, int ownerId, CancellationToken ct);

    Task DeleteAsync(int attachmentId, CancellationToken ct);

    /// <summary>Tải nội dung file qua backend (thay vì presigned URL) — trả kèm tên file gốc để set Content-Disposition.</summary>
    Task<(FileDownloadResult Result, string FileName)> DownloadAsync(int attachmentId, CancellationToken ct);
}
