using MPS.Application.Common.Models.Evaluation;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates.Evaluation;

/// <summary>Phase 6 — Đánh giá tháng công bố: gửi thẳng cho NV được đánh giá.</summary>
public sealed class MonthlyEvaluationPublishedTemplate : NotificationTemplate<MonthlyEvaluationPublishedNotification>
{
    public override NotificationType Type => NotificationType.MonthlyEvaluationPublished;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        MonthlyEvaluationPublishedNotification e, CancellationToken ct)
    {
        var content = Content(
            category: NotificationCategory.System,
            title: $"Đánh giá tháng {e.Month:D2}/{e.Year} đã công bố",
            message: $"Điểm chung: {e.FinalScore:0.##}. Xem chi tiết để đối chiếu.",
            linkUrl: $"/evaluations/monthly/{e.MonthlyEvaluationId}");

        var email = new EmailContent(
            Subject: $"[Mingo Payroll] Đánh giá tháng {e.Month:D2}/{e.Year}",
            TextBody:
                $"Đánh giá tháng {e.Month:D2}/{e.Year} của bạn đã công bố.{Environment.NewLine}" +
                $"Điểm chung: {e.FinalScore:0.##}");

        return SingleWithEmail(RecipientSpec.Users(e.EmployeeUserId), content, email);
    }
}
