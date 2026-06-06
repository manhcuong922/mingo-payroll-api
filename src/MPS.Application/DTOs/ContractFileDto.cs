namespace MPS.Application.DTOs;

/// <summary>
/// File metadata trả về cho FE, kèm presigned URL (TTL 15 phút mặc định)
/// để tải PDF từ MinIO không cần proxy qua backend.
/// </summary>
/// <remarks>
/// <para><b>SignedAt KHÔNG có ở row này</b> vì ContractFile schema không lưu — thời điểm
/// ký được lưu trên <c>Contract.SignedAt</c> (cấp HĐ, không phải cấp file). Dùng
/// <see cref="UploadedAt"/> cho timestamp file.</para>
/// <para><see cref="TargetType"/> = "Contract" | "Appendix" | "Termination" — FE phân loại
/// để hiển thị (icon, label) khi 1 endpoint trả mix nhiều loại.</para>
/// </remarks>
public sealed record ContractFileDto(
    int Id,
    string TargetType,
    int TargetId,
    string FileName,
    string MimeType,
    long FileSizeBytes,
    string Sha256,
    int Version,
    bool IsCurrent,
    string? SignedBySource,
    DateTime UploadedAt,
    int UploadedByUserId,
    string PresignedDownloadUrl);
