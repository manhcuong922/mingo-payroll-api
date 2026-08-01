using MPS.Application.Common.Models.Compliance;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates.Compliance;

/// <summary>Phase 5 — bản pending sắp tự active (T-7 / T-1): báo cho HR, Kế toán, Compliance.</summary>
public sealed class CompliancePendingSoonTemplate : NotificationTemplate<CompliancePendingSoonNotification>
{
    public override NotificationType Type => NotificationType.CompliancePendingSoon;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        CompliancePendingSoonNotification e, CancellationToken ct)
    {
        var content = Content(
            category: NotificationCategory.System,
            title: $"{e.DisplayName} sẽ tự active sau {e.DaysRemaining} ngày",
            message: $"Bản mới có hiệu lực từ {e.EffectiveFrom:dd/MM/yyyy}. Vui lòng kiểm tra lại.",
            linkUrl: $"/compliance/{e.ComplianceType.ToLowerInvariant()}");

        var email = new EmailContent(
            Subject: $"[Mingo Payroll] {e.DisplayName} sắp active ({e.DaysRemaining} ngày)",
            TextBody:
                $"{e.DisplayName}{Environment.NewLine}" +
                $"Hiệu lực từ: {e.EffectiveFrom:dd/MM/yyyy}{Environment.NewLine}" +
                $"Còn: {e.DaysRemaining} ngày");

        return SingleWithEmail(
            RecipientSpec.Permissions(
                Permissions.LaborLawConstantsUpdateAll,
                Permissions.PayrollReadAll),
            content, email);
    }
}
