using MPS.Application.Common.Models.Compliance;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates.Compliance;

/// <summary>Phase 5 — Auto-Update phát hiện văn bản pháp luật mới.</summary>
public sealed class LegalDocumentDetectedTemplate : NotificationTemplate<LegalDocumentDetectedNotification>
{
    public override NotificationType Type => NotificationType.LegalDocumentDetected;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        LegalDocumentDetectedNotification e, CancellationToken ct)
    {
        var content = Content(
            category: NotificationCategory.System,
            title: "Phát hiện văn bản pháp luật mới",
            message: e.DocumentTitle,
            linkUrl: $"/compliance/auto-update/documents/{e.LegalDocumentId}");

        var email = new EmailContent(
            Subject: $"[Mingo Payroll] Văn bản pháp luật mới: {e.DocumentTitle}",
            TextBody:
                $"{e.DocumentTitle}{Environment.NewLine}" +
                $"Nguồn: {e.SourceUrl}");

        return SingleWithEmail(
            RecipientSpec.Permissions(Permissions.ComplianceAutoUpdateReadAll),
            content, email);
    }
}
