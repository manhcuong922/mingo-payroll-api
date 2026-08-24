using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Features.ProjectTask;

internal static class AttachmentMapper
{
    // Metadata (không có presigned URL) — dùng khi nhúng trong cây task/đơn.
    public static ProductionAttachmentDto ToDtoMetadata(ProductionAttachment a) =>
        ToDto(a, downloadUrl: string.Empty);

    // Đầy đủ — dùng ở endpoint list attachment (service tự resolve presigned URL).
    public static ProductionAttachmentDto ToDto(ProductionAttachment a, string downloadUrl) =>
        new(
            a.Id,
            a.OwnerType.ToString(),
            a.OwnerId,
            a.FileName,
            a.ContentType,
            a.SizeBytes,
            a.Note,
            downloadUrl,
            a.UploadedBy?.FullName,
            a.UploadedAt);
}
