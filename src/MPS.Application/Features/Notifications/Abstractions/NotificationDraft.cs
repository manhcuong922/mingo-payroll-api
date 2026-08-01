using MPS.Application.Features.Notifications.Templates;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Abstractions;

/// <summary>
/// Bản mô tả 1 thông báo cần tạo (đầu vào cho INotificationService).
/// Template (Phase 3) sinh ra danh sách draft này; service chịu trách nhiệm lưu DB + đẩy realtime.
///
/// Phase 1 mở kênh email: nếu <see cref="Email"/> != null và <see cref="DeliveryMode"/> cho phép,
/// service sẽ dispatch email cho người nhận sau khi lưu in-app (best-effort — email fail không
/// ảnh hưởng in-app đã lưu).
/// </summary>
public sealed record NotificationDraft(
    int RecipientUserId,
    NotificationType Type,
    NotificationCategory Category,
    NotificationSeverity Severity,
    string Title,
    string Message,
    string? LinkUrl = null,
    string? MetadataJson = null,
    EmailContent? Email = null,
    NotificationDeliveryMode DeliveryMode = NotificationDeliveryMode.Default,
    bool PrefersPersonalEmail = false);
