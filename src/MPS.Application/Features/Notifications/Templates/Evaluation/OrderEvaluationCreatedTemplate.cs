using MPS.Application.Common.Models.Evaluation;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates.Evaluation;

/// <summary>
/// Phase 6 — Bridge từ <see cref="ProjectCompletedNotification"/> (đã publish sẵn): báo cho user
/// có quyền xem OrderEvaluation.
/// </summary>
public sealed class OrderEvaluationCreatedTemplate : NotificationTemplate<ProjectCompletedNotification>
{
    public override NotificationType Type => NotificationType.OrderEvaluationCreated;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        ProjectCompletedNotification e, CancellationToken ct)
    {
        var content = Content(
            category: NotificationCategory.System,
            title: $"Đơn hàng #{e.ProjectId} đã hoàn tất — cần đánh giá",
            message: "Bảng đánh giá theo đơn đã được tạo. Vui lòng vào chấm điểm.",
            linkUrl: $"/evaluations/orders?projectId={e.ProjectId}");

        var email = new EmailContent(
            Subject: $"[Mingo Payroll] Đơn hàng #{e.ProjectId} — cần đánh giá",
            TextBody: $"Đơn hàng #{e.ProjectId} vừa hoàn tất. Bảng đánh giá đã sẵn sàng, mời bạn chấm điểm.");

        return SingleWithEmail(
            RecipientSpec.Permissions(Permissions.OrderEvaluationReadAll),
            content, email);
    }
}
