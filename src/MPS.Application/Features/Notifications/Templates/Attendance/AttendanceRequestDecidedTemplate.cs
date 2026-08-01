using MPS.Application.Common.Models.Notifications;
using MPS.Application.Features.Attendance.Common;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates.Attendance;

/// <summary>
/// Phase 3 §N2: đơn duyệt/từ chối → báo cho người submit; nếu NV chủ đơn ≠ submit (HR nộp hộ)
/// thì gửi cho cả 2. Email luôn gửi khi bị từ chối; khi duyệt chỉ gửi email (không dồn user với IA).
/// </summary>
public sealed class AttendanceRequestDecidedTemplate
    : NotificationTemplate<AttendanceRequestDecidedNotification>
{
    public override NotificationType Type => NotificationType.AttendanceRequestDecided;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        AttendanceRequestDecidedNotification e, CancellationToken ct)
    {
        var (_, kindLabel, link) = AttendanceRequestSubmittedTemplate.KindDescriptor(e.Kind, e.RequestId);
        var isApproved = e.Decision == AttendanceRequestDecision.Approved;
        var decisionWord = isApproved ? "đã được duyệt" : "đã bị từ chối";
        var severity = isApproved ? NotificationSeverity.Success : NotificationSeverity.Warning;

        var content = Content(
            category: NotificationCategory.Leave,
            title: $"{kindLabel} #{e.RequestId} {decisionWord}",
            message: e.Summary + (string.IsNullOrWhiteSpace(e.Reason) ? "" : $" — Lý do: {e.Reason}"),
            linkUrl: link,
            severity: severity);

        var email = new EmailContent(
            Subject: $"[Mingo Payroll] {kindLabel} #{e.RequestId} {decisionWord}",
            TextBody:
                $"{e.Summary}{Environment.NewLine}" +
                (string.IsNullOrWhiteSpace(e.Reason) ? "" : $"Lý do: {e.Reason}{Environment.NewLine}") +
                $"{Environment.NewLine}Xem chi tiết: {link}");

        // Gộp submitter + requester (nếu khác) — distinct để tránh gửi 2 lần cùng 1 user.
        var recipients = RecipientSpec.Users(new HashSet<int> { e.SubmittedByUserId, e.RequesterUserId });

        // Khi bị từ chối luôn gửi cả email; khi được duyệt chỉ IA (giảm spam mail cho các đơn nhỏ).
        return isApproved
            ? Single(recipients, content)
            : SingleWithEmail(recipients, content, email);
    }
}
