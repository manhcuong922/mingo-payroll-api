using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Abstractions;

/// <summary>
/// Bản mô tả 1 thông báo cần tạo (đầu vào cho INotificationService).
/// Template (Phase 3) sinh ra danh sách draft này; service chịu trách nhiệm lưu DB + đẩy realtime.
/// </summary>
public sealed record NotificationDraft(
    int RecipientUserId,
    NotificationType Type,
    NotificationCategory Category,
    NotificationSeverity Severity,
    string Title,
    string Message,
    string? LinkUrl = null,
    string? MetadataJson = null);
