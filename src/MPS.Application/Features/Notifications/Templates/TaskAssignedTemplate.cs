using MPS.Application.Common.Models.Notifications;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates;

/// <summary>
/// Planning giao đơn/việc cho nhân sự → chỉ những người ĐƯỢC GIAO nhận thông báo (trừ người giao).
/// </summary>
public sealed class TaskAssignedTemplate : NotificationTemplate<TaskAssignedDomainNotification>
{
    public override NotificationType Type => NotificationType.TaskAssigned;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        TaskAssignedDomainNotification e, CancellationToken ct)
    {
        if (e.AssigneeUserIds.Count == 0) return Nothing();

        var link = e.ProjectId is int pid ? $"/projects/{pid}/tasks/{e.TaskId}" : $"/tasks/{e.TaskId}";

        var content = Content(
            category: NotificationCategory.Task,
            title: "Bạn được giao công việc mới",
            message: $"Bạn vừa được giao: \"{e.Title}\".",
            linkUrl: link,
            severity: NotificationSeverity.Info);

        // Người nhận = người được giao, loại trừ người thực hiện thao tác giao.
        var recipients = RecipientSpec.Users(e.AssigneeUserIds).Excluding(e.AssignedByUserId);

        return Single(recipients, content);
    }
}
