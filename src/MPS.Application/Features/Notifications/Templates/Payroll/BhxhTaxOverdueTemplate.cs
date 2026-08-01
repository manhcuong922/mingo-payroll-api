using MPS.Application.Common.Models.Payroll;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates.Payroll;

/// <summary>
/// Phase 4 §N9 — BHXH / thuế đã quá hạn: gửi HẰNG NGÀY (Critical) cho tới khi obligation chuyển Paid.
/// Recipient gộp cả record + confirm để trưởng phòng KT không bỏ sót.
/// </summary>
public sealed class BhxhTaxOverdueTemplate : NotificationTemplate<BhxhTaxOverdueNotification>
{
    public override NotificationType Type => NotificationType.BhxhTaxOverdue;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        BhxhTaxOverdueNotification e, CancellationToken ct)
    {
        var content = Content(
            category: NotificationCategory.System,
            title: $"{e.ObligationLabel} ĐÃ QUÁ HẠN {e.DaysOverdue} ngày",
            message: $"Số tiền {e.Amount:N0} {e.Currency}, hạn {e.DueDate:dd/MM/yyyy}. Cần xử lý ngay.",
            linkUrl: $"/payroll/obligations/{e.ObligationId}",
            severity: NotificationSeverity.Critical);

        var email = new EmailContent(
            Subject: $"[Mingo Payroll][CRITICAL] {e.ObligationLabel} quá hạn {e.DaysOverdue} ngày",
            TextBody:
                $"CẢNH BÁO: {e.ObligationLabel} đã quá hạn {e.DaysOverdue} ngày.{Environment.NewLine}" +
                $"Số tiền: {e.Amount:N0} {e.Currency}{Environment.NewLine}" +
                $"Hạn nộp: {e.DueDate:dd/MM/yyyy}{Environment.NewLine}" +
                $"Vui lòng xử lý ngay hôm nay.");

        return SingleWithEmail(
            RecipientSpec.Permissions(
                Permissions.BhxhTaxPaymentRecordAll,
                Permissions.BhxhTaxPaymentConfirmAll),
            content, email);
    }
}
