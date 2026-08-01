using MPS.Application.Common.Models.Notifications;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates.Employee;

/// <summary>
/// Phase 6 §N12 — field nhạy cảm (CCCD/STK/TaxNo/SocialNo/Dependents) vừa bị chỉnh: cảnh báo NV
/// (email khi có tài khoản) + audit cho HR có quyền update all. Người thực hiện thay đổi bị loại
/// khỏi recipient để không tự báo cho chính mình.
/// </summary>
public sealed class EmployeeSensitiveEditedTemplate : NotificationTemplate<EmployeeSensitiveEditedNotification>
{
    public override NotificationType Type => NotificationType.EmployeeSensitiveEdited;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        EmployeeSensitiveEditedNotification e, CancellationToken ct)
    {
        var fieldsText = string.Join(", ", e.ChangedFields);
        var content = Content(
            category: NotificationCategory.System,
            title: "Thông tin nhạy cảm bị chỉnh",
            message: $"Các field vừa đổi: {fieldsText}. Nếu không phải bạn thực hiện, vui lòng liên hệ HR ngay.",
            linkUrl: $"/employees/{e.EmployeeInformationId}",
            severity: NotificationSeverity.Warning);

        var email = new EmailContent(
            Subject: "[Mingo Payroll] Cảnh báo: thông tin cá nhân nhạy cảm bị chỉnh",
            TextBody:
                $"Hệ thống ghi nhận các field sau vừa bị chỉnh: {fieldsText}.{Environment.NewLine}" +
                "Nếu không phải bạn thực hiện, vui lòng liên hệ HR ngay.");

        var items = new List<NotificationPlanItem>
        {
            // Audit cho HR.
            PlanWithEmail(
                RecipientSpec.Permissions(Permissions.EmployeeInformationUpdateAll).Excluding(e.ChangedByUserId),
                content, email),
        };

        // Báo chính NV (nếu có tài khoản).
        if (e.EmployeeUserId.HasValue && e.EmployeeUserId.Value != e.ChangedByUserId)
        {
            items.Add(PlanWithEmail(
                RecipientSpec.Users(e.EmployeeUserId.Value),
                content, email));
        }

        return Many(items.ToArray());
    }
}
