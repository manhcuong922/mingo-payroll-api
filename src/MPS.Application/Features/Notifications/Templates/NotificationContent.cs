using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates;

/// <summary>
/// Nội dung hiển thị của 1 thông báo (chưa gắn người nhận). Template sinh ra content này.
/// Type lấy từ template nên không cần lặp lại ở đây.
/// </summary>
public sealed record NotificationContent(
    NotificationCategory Category,
    NotificationSeverity Severity,
    string Title,
    string Message,
    string? LinkUrl = null,
    string? MetadataJson = null);
