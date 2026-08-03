using System.Globalization;
using MPS.Application.Common.Models.Payroll;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Enums.Notifications;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Features.Notifications.Templates.Payroll;

/// <summary>
/// v3.1 — HR/Accountant ghi nhận đã nộp BHXH/TNCN của 1 NV → thông báo in-app cho chính NV đó.
/// Nếu NV chưa có tài khoản (<c>EmployeeUserId == null</c>) → không phát (Nothing).
/// </summary>
public sealed class BhxhTaxLinePaidTemplate : NotificationTemplate<BhxhTaxLinePaidNotification>
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public override NotificationType Type => NotificationType.BhxhTaxLinePaid;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        BhxhTaxLinePaidNotification e, CancellationToken ct)
    {
        if (e.EmployeeUserId is not int userId)
            return Nothing();

        var label = FormatLabel(e.ObligationType);
        var period = $"{e.PeriodMonth:D2}/{e.PeriodYear}";
        var amount = e.PaidAmount.ToString("N0", ViCulture);
        var paidDate = e.PaidDate.ToString("dd/MM/yyyy", ViCulture);

        var content = Content(
            category: NotificationCategory.System,
            title: $"Đã ghi nhận nộp {label} tháng {period}",
            message: $"Khoản đóng {label} tháng {period} của bạn đã được ghi nhận nộp {amount} VNĐ vào ngày {paidDate}.",
            linkUrl: $"/payroll/periods/my-payslip?year={e.PeriodYear}&month={e.PeriodMonth}",
            severity: NotificationSeverity.Info);

        return Single(RecipientSpec.Users(userId), content);
    }

    private static string FormatLabel(PaymentObligationType type) => type switch
    {
        PaymentObligationType.Bhxh => "BHXH",
        PaymentObligationType.Tncn => "TNCN",
        _ => type.ToString(),
    };
}
