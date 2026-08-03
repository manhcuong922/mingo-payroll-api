using System.Globalization;
using MPS.Application.Common.Models.Payroll;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Enums.Notifications;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Features.Notifications.Templates.Payroll;

/// <summary>
/// v3.1 — HR/Accountant đánh dấu bỏ qua line BHXH/TNCN của 1 NV → thông báo in-app cho NV đó
/// với lý do audit. Severity Warning để user chú ý (không phải là "đã nộp" bình thường).
/// </summary>
public sealed class BhxhTaxLineExcludedTemplate : NotificationTemplate<BhxhTaxLineExcludedNotification>
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public override NotificationType Type => NotificationType.BhxhTaxLineExcluded;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        BhxhTaxLineExcludedNotification e, CancellationToken ct)
    {
        if (e.EmployeeUserId is not int userId)
            return Nothing();

        var label = FormatLabel(e.ObligationType);
        var period = $"{e.PeriodMonth:D2}/{e.PeriodYear}";

        var content = Content(
            category: NotificationCategory.System,
            title: $"Bỏ qua kỳ nộp {label} tháng {period}",
            message: $"Khoản {label} tháng {period} của bạn đã được đánh dấu bỏ qua. Lý do: {e.ExcludedReason}",
            linkUrl: $"/payroll/periods/my-payslip?year={e.PeriodYear}&month={e.PeriodMonth}",
            severity: NotificationSeverity.Warning);

        return Single(RecipientSpec.Users(userId), content);
    }

    private static string FormatLabel(PaymentObligationType type) => type switch
    {
        PaymentObligationType.Bhxh => "BHXH",
        PaymentObligationType.Tncn => "TNCN",
        _ => type.ToString(),
    };
}
