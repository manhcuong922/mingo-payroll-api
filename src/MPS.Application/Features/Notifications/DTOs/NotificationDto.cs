using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.DTOs;

/// <summary>
/// DTO trả về cho FE (chuông). Enum serialize thành chuỗi nhờ JsonStringEnumConverter đã cấu hình ở Program.
/// </summary>
public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    NotificationCategory Category,
    NotificationSeverity Severity,
    string Title,
    string Message,
    string? LinkUrl,
    string? MetadataJson,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt);
