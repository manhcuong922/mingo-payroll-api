using MPS.Application.Common.Models.Attendance;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates.Employee;

/// <summary>
/// Phase 6 — Đổi phòng ban: báo user có quyền đọc EI theo dept (cả dept cũ + mới nếu có).
/// EmployeeUserId không đi kèm event; template chỉ báo audit HR — NV chủ sẽ nhận qua channel khác nếu cần.
/// </summary>
public sealed class EmployeeDepartmentChangedTemplate : NotificationTemplate<EmployeeDepartmentChangedNotification>
{
    public override NotificationType Type => NotificationType.EmployeeDepartmentChanged;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        EmployeeDepartmentChangedNotification e, CancellationToken ct)
    {
        var content = Content(
            category: NotificationCategory.System,
            title: "Điều chuyển phòng ban",
            message: $"NV #{e.EmployeeInformationId} đổi từ dept {e.OldDepartmentId?.ToString() ?? "(chưa gán)"} → {e.NewDepartmentId?.ToString() ?? "(gỡ)"}.",
            linkUrl: $"/employees/{e.EmployeeInformationId}");

        var email = new EmailContent(
            Subject: "[Mingo Payroll] Điều chuyển phòng ban",
            TextBody:
                $"NV #{e.EmployeeInformationId} đổi phòng ban: {e.OldDepartmentId?.ToString() ?? "(chưa gán)"} → {e.NewDepartmentId?.ToString() ?? "(gỡ)"}.");

        return SingleWithEmail(
            RecipientSpec.Permissions(
                Permissions.EmployeeInformationUpdateAll,
                Permissions.EmployeeInformationReadDepartment)
                .Excluding(e.ChangedByUserId),
            content, email);
    }
}
