using MPS.Domain.Enums;

namespace MPS.Domain.Entities;

/// <summary>
/// Hàng đợi email dựa trên DB (transactional outbox). API ghi một dòng Pending; worker
/// <c>EmailDispatchJob</c> poll định kỳ, gửi qua <c>IRealEmailSender</c> rồi cập nhật trạng thái.
/// PDF KHÔNG lưu ở đây — chỉ lưu <see cref="StorageBucket"/>/<see cref="StorageObjectKey"/> để worker tự tải từ MinIO.
/// </summary>
public class EmailOutboxMessage
{
    public int Id { get; set; }

    public EmailOutboxType Type { get; set; }

    public string ToEmail { get; set; } = string.Empty;

    /// <summary>Tham số render template, serialize JSON. Bị xóa (rỗng) sau khi gửi thành công để không giữ dữ liệu nhạy cảm.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>Chỉ dùng cho <see cref="EmailOutboxType.ContractActivation"/> — vị trí PDF trên MinIO.</summary>
    public string? StorageBucket { get; set; }
    public string? StorageObjectKey { get; set; }

    public EmailOutboxStatus Status { get; set; } = EmailOutboxStatus.Pending;

    public int Attempts { get; set; }

    /// <summary>Mốc sớm nhất được thử lại (exponential backoff). Null = có thể gửi ngay.</summary>
    public DateTime? NextAttemptAt { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? SentAt { get; set; }
}
