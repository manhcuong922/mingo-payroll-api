using MPS.Application.Common.Models.Payroll;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates.Payroll;

/// <summary>Phase 4 §N9 — BHXH / thuế sắp đến hạn (T-7, T-3, T-1). Recipient theo permission.</summary>
public sealed class BhxhTaxDeadlineSoonTemplate : NotificationTemplate<BhxhTaxDeadlineSoonNotification>
{
    public override NotificationType Type => NotificationType.BhxhTaxDeadlineSoon;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        BhxhTaxDeadlineSoonNotification e, CancellationToken ct)
    {
        var severity = e.DaysRemaining <= 1 ? NotificationSeverity.Warning : NotificationSeverity.Info;
        var content = Content(
            category: NotificationCategory.System,
            title: $"{e.ObligationLabel} còn {e.DaysRemaining} ngày tới hạn",
            message: $"Số tiền {e.Amount:N0} {e.Currency}, hạn {e.DueDate:dd/MM/yyyy}.",
            linkUrl: $"/payroll/obligations/{e.ObligationId}",
            severity: severity);

        var email = new EmailContent(
            Subject: $"[Mingo Payroll] {e.ObligationLabel} còn {e.DaysRemaining} ngày (hạn {e.DueDate:dd/MM/yyyy})",
            TextBody:
                $"{e.ObligationLabel}{Environment.NewLine}" +
                $"Số tiền: {e.Amount:N0} {e.Currency}{Environment.NewLine}" +
                $"Hạn nộp: {e.DueDate:dd/MM/yyyy}{Environment.NewLine}" +
                $"Còn: {e.DaysRemaining} ngày");

        return SingleWithEmail(
            RecipientSpec.Permissions(Permissions.BhxhTaxPaymentRecordAll),
            content, email);
    }
}
