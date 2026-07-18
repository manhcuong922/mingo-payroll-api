namespace MPS.Application.DTOs;

public record ProductionAttachmentDto(
    int Id,
    string OwnerType,
    int OwnerId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Note,
    string DownloadUrl,                 // presigned URL (hết hạn theo cấu hình MinIO)
    string? UploadedByName,
    DateTime UploadedAt);
