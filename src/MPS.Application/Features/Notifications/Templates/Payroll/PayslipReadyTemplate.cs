using MPS.Application.Common.Models.Payroll;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates.Payroll;

/// <summary>Phase 4 §N11 — payslip cá nhân sẵn sàng: gửi thẳng cho NV (IA + Email).</summary>
public sealed class PayslipReadyTemplate : NotificationTemplate<PayslipReadyNotification>
{
    public override NotificationType Type => NotificationType.PayslipReady;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        PayslipReadyNotification e, CancellationToken ct)
    {
        var content = Content(
            category: NotificationCategory.System,
            title: $"Payslip {e.PeriodMonth:D2}/{e.PeriodYear} đã sẵn sàng",
            message: $"Bạn có thể xem chi tiết bảng lương tháng {e.PeriodMonth:D2}/{e.PeriodYear}.",
            linkUrl: $"/payroll/periods/{e.PeriodId}/my-payslip");

        var email = new EmailContent(
            Subject: $"[Mingo Payroll] Payslip tháng {e.PeriodMonth:D2}/{e.PeriodYear} đã sẵn sàng",
            TextBody:
                $"Bảng lương tháng {e.PeriodMonth:D2}/{e.PeriodYear} của bạn đã sẵn sàng xem.{Environment.NewLine}" +
                $"Vui lòng đăng nhập hệ thống để đối chiếu chi tiết.");

        return SingleWithEmail(RecipientSpec.Users(e.EmployeeUserId), content, email);
    }
}
