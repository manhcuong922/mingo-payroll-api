using System.Text.Json;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates;

/// <summary>
/// Luật/quy định thay đổi → thông báo TOÀN BỘ nhân sự. Nếu có thay đổi số cụ thể thì liệt kê
/// "từ A thành B"; nếu không, hiển thị thông báo cập nhật chung.
/// </summary>
public sealed class ComplianceChangedTemplate : NotificationTemplate<ComplianceVersionChangedNotification>
{
    // Tên hiển thị tiếng Việt theo loại compliance (fallback khi handler nguồn không truyền DisplayName).
    private static readonly IReadOnlyDictionary<string, string> FriendlyNames = new Dictionary<string, string>
    {
        [nameof(MinimumWageVersion)] = "Lương tối thiểu vùng",
        [nameof(LaborLawConstantsVersion)] = "Hằng số luật lao động",
        [nameof(PitRegulationVersion)] = "Thuế thu nhập cá nhân",
        [nameof(SocialInsuranceRateVersion)] = "Tỷ lệ bảo hiểm xã hội",
        [nameof(AllowanceExemptThresholdVersion)] = "Ngưỡng phụ cấp miễn thuế",
    };

    public override NotificationType Type => NotificationType.System;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        ComplianceVersionChangedNotification e, CancellationToken ct)
    {
        var name = e.DisplayName
            ?? (FriendlyNames.TryGetValue(e.ComplianceType, out var friendly) ? friendly : "Quy định");

        var changes = e.Changes ?? Array.Empty<ComplianceFieldChange>();

        string message;
        string? metadataJson = null;

        if (changes.Count > 0)
        {
            message = "Thay đổi: " + string.Join("; ",
                changes.Select(c => $"{c.Label} từ {c.OldValue} thành {c.NewValue}")) + ".";

            metadataJson = JsonSerializer.Serialize(new
            {
                complianceType = e.ComplianceType,
                changes = changes.Select(c => new { c.Label, c.OldValue, c.NewValue }),
            });
        }
        else
        {
            message = $"Quy định \"{name}\" vừa được cập nhật. Vui lòng xem chi tiết.";
        }

        var content = Content(
            category: NotificationCategory.Compliance,
            title: $"Cập nhật quy định: {name}",
            message: message,
            linkUrl: "/compliance",
            severity: NotificationSeverity.Warning,
            metadataJson: metadataJson);

        // Phase 5 §N1 — thay vì Everyone (spam toàn công ty), chỉ gửi cho HR/Kế toán/Compliance/quản lý
        // (user có ít nhất 1 trong các permission read/update compliance HOẶC đọc payroll).
        var recipients = RecipientSpec.Permissions(
            Permissions.LaborLawConstantsReadAll,
            Permissions.LaborLawConstantsUpdateAll,
            Permissions.PitRegulationReadAll,
            Permissions.PitRegulationUpdateAll,
            Permissions.SocialInsuranceRateReadAll,
            Permissions.SocialInsuranceRateUpdateAll,
            Permissions.MinimumWageReadAll,
            Permissions.MinimumWageUpdateAll,
            Permissions.AllowanceExemptThresholdReadAll,
            Permissions.AllowanceExemptThresholdUpdateAll,
            Permissions.PayrollReadAll);

        var email = new EmailContent(
            Subject: $"[Mingo Payroll] Cập nhật quy định: {name}",
            TextBody: message);

        return SingleWithEmail(recipients, content, email);
    }
}
