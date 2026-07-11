using MPS.Application.Features.Notifications.DTOs;
using MPS.Domain.Entities.Notifications;

namespace MPS.Application.Features.Notifications;

/// <summary>
/// Map thủ công Notification -> NotificationDto. Giữ module tự chứa, không phụ thuộc AutoMapper profile.
/// </summary>
public static class NotificationMappingExtensions
{
    public static NotificationDto ToDto(this Notification n) => new(
        n.Id,
        n.Type,
        n.Category,
        n.Severity,
        n.Title,
        n.Message,
        n.LinkUrl,
        n.MetadataJson,
        n.IsRead,
        n.ReadAt,
        n.CreatedAt);
}
