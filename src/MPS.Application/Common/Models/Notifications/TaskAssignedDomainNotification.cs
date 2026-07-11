using MediatR;

namespace MPS.Application.Common.Models.Notifications;

/// <summary>
/// Sự kiện: một công việc/đơn vừa được giao cho (các) nhân sự. Những người trong
/// <paramref name="AssigneeUserIds"/> sẽ nhận thông báo (trừ người giao).
/// Module Task chỉ publish sự kiện mỏng; logic thông báo nằm ở module Notification.
/// </summary>
public sealed record TaskAssignedDomainNotification(
    int TaskId,
    string Title,
    IReadOnlyCollection<int> AssigneeUserIds,
    int? AssignedByUserId,
    int? ProjectId) : INotification;
