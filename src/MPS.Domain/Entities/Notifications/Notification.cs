using MPS.Domain.Common;
using MPS.Domain.Enums.Notifications;

namespace MPS.Domain.Entities.Notifications;

/// <summary>
/// Thông báo in-app hiển thị ở chuông trên giao diện.
/// Thuộc bounded context RIÊNG (NotificationDbContext) — bảng riêng, KHÔNG FK cứng sang User
/// để giữ độc lập giữa 2 DbContext. Nguồn sự thật là DB; SignalR chỉ đẩy realtime bổ sung.
/// </summary>
public class Notification : BaseEntity   // Id (Guid), CreatedAt, UpdatedAt
{
    /// <summary>Id người nhận (khớp claim NameIdentifier = int UserId). Không navigation.</summary>
    public int RecipientUserId { get; set; }

    public NotificationType Type { get; set; }
    public NotificationCategory Category { get; set; }
    public NotificationSeverity Severity { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>Đường dẫn điều hướng khi click chuông (vd "/projects/123"). Tùy chọn.</summary>
    public string? LinkUrl { get; set; }

    /// <summary>Payload phụ (các id liên quan...) dạng JSON. Tùy chọn.</summary>
    public string? MetadataJson { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
