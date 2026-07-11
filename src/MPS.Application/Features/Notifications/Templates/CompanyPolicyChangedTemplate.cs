using MPS.Application.Common.Models.Notifications;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates;

/// <summary>
/// Nội quy công ty được thêm/sửa/tải bản mới → thông báo TOÀN BỘ nhân sự.
/// </summary>
public sealed class CompanyPolicyChangedTemplate : NotificationTemplate<CompanyPolicyChangedNotification>
{
    public override NotificationType Type => NotificationType.System;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        CompanyPolicyChangedNotification e, CancellationToken ct)
    {
        var (title, message) = e.Kind switch
        {
            CompanyPolicyChangeKind.Created => (
                "Nội quy công ty mới",
                $"Vừa ban hành nội quy: \"{e.Title}\". Vui lòng xem chi tiết."),
            CompanyPolicyChangeKind.NewVersion => (
                "Nội quy công ty cập nhật",
                $"Nội quy \"{e.Title}\" vừa có phiên bản mới. Vui lòng xem chi tiết."),
            _ => (
                "Nội quy công ty thay đổi",
                $"Nội quy \"{e.Title}\" vừa được chỉnh sửa. Vui lòng xem chi tiết."),
        };

        var content = Content(
            category: NotificationCategory.System,
            title: title,
            message: message,
            linkUrl: $"/company-policies/{e.PolicyId}",
            severity: NotificationSeverity.Info);

        return Single(RecipientSpec.Everyone(), content);
    }
}
