using MPS.Application.Common.Models.Notifications;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates.Employee;

/// <summary>Phase 6 — Đổi bậc lương: báo cho NV + Kế toán + user có quyền update SalaryGrade.</summary>
public sealed class SalaryGradeChangedTemplate : NotificationTemplate<SalaryGradeChangedNotification>
{
    public override NotificationType Type => NotificationType.SalaryGradeChanged;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        SalaryGradeChangedNotification e, CancellationToken ct)
    {
        var delta = e.NewBaseSalary - e.OldBaseSalary;
        var deltaSign = delta >= 0 ? "+" : string.Empty;
        var content = Content(
            category: NotificationCategory.System,
            title: "Cập nhật bậc lương",
            message: $"Bậc lương từ {e.OldBaseSalary:N0} → {e.NewBaseSalary:N0} {e.Currency} ({deltaSign}{delta:N0}).",
            linkUrl: $"/employees/{e.EmployeeInformationId}",
            severity: NotificationSeverity.Info);

        var email = new EmailContent(
            Subject: "[Mingo Payroll] Cập nhật bậc lương",
            TextBody:
                $"Bậc lương: {e.OldBaseSalary:N0} → {e.NewBaseSalary:N0} {e.Currency}{Environment.NewLine}" +
                $"Chênh lệch: {deltaSign}{delta:N0} {e.Currency}");

        // Nội bộ: KT + user có SalaryGrade.Update.All.
        var internalSpec = RecipientSpec.Permissions(
            Permissions.PayrollReadAll,
            Permissions.SalaryGradeUpdateAll)
            .Excluding(e.ChangedByUserId);

        var items = new List<NotificationPlanItem>
        {
            PlanWithEmail(internalSpec, content, email),
        };

        if (e.EmployeeUserId.HasValue && e.EmployeeUserId.Value != e.ChangedByUserId)
            items.Add(PlanWithEmail(RecipientSpec.Users(e.EmployeeUserId.Value), content, email));

        return Many(items.ToArray());
    }
}
