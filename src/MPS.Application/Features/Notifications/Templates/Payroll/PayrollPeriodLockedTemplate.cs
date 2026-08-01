using MPS.Application.Common.Models.Payroll;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates.Payroll;

/// <summary>Phase 4 §N11 — khoá kỳ lương: báo cho user có <c>Payroll.Finalize.All</c>.</summary>
public sealed class PayrollPeriodLockedTemplate : NotificationTemplate<PayrollPeriodLockedNotification>
{
    public override NotificationType Type => NotificationType.PayrollPeriodLocked;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        PayrollPeriodLockedNotification e, CancellationToken ct)
    {
        var content = Content(
            category: NotificationCategory.System,
            title: $"Kỳ lương {e.PeriodMonth:D2}/{e.PeriodYear} đã khoá",
            message: "Kỳ lương đã chuyển sang Locked — không chỉnh sửa được nữa.",
            linkUrl: $"/payroll/periods/{e.PeriodId}");

        var email = new EmailContent(
            Subject: $"[Mingo Payroll] Kỳ lương {e.PeriodMonth:D2}/{e.PeriodYear} đã khoá",
            TextBody: $"Kỳ {e.PeriodMonth:D2}/{e.PeriodYear} vừa được khoá.");

        return SingleWithEmail(
            RecipientSpec.Permissions(Permissions.PayrollFinalizeAll).Excluding(e.LockedByUserId),
            content, email);
    }
}
