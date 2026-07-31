using MPS.Application.Common.Models.Compliance;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates.Compliance;

/// <summary>Phase 5 — Auto-Update tạo đề xuất chờ compliance officer duyệt.</summary>
public sealed class ProposalPendingReviewTemplate : NotificationTemplate<ProposalPendingReviewNotification>
{
    public override NotificationType Type => NotificationType.ProposalPendingReview;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        ProposalPendingReviewNotification e, CancellationToken ct)
    {
        var content = Content(
            category: NotificationCategory.System,
            title: $"Đề xuất Compliance mới ({e.ComplianceType}) chờ duyệt",
            message: e.Summary,
            linkUrl: $"/compliance/auto-update/proposals/{e.ProposalId}");

        var email = new EmailContent(
            Subject: $"[Mingo Payroll] Đề xuất Compliance chờ duyệt (#{e.ProposalId})",
            TextBody: $"{e.ComplianceType}{Environment.NewLine}{e.Summary}");

        return SingleWithEmail(
            RecipientSpec.Permissions(Permissions.ComplianceAutoUpdateApproveAll),
            content, email);
    }
}
