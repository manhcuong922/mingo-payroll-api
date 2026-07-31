using MPS.Application.Common.Models.Notifications;
using MPS.Application.Features.Attendance.Common;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates.Attendance;

/// <summary>
/// Phase 3 §N2: đơn nộp mới → thông báo NGƯỜI DUYỆT được gán trong đơn (không phải role name).
/// Approver có thể null khi flow duyệt chưa gán ngay (VD OT PmReview) — khi đó bỏ qua thông báo submit.
/// </summary>
public sealed class AttendanceRequestSubmittedTemplate
    : NotificationTemplate<AttendanceRequestSubmittedNotification>
{
    public override NotificationType Type => NotificationType.AttendanceRequestSubmitted;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        AttendanceRequestSubmittedNotification e, CancellationToken ct)
    {
        if (e.ApproverUserIds.Count == 0)
            return Nothing();

        var (title, kindLabel, link) = KindDescriptor(e.Kind, e.RequestId);
        var content = Content(
            category: NotificationCategory.Leave,
            title: title,
            message: $"{e.Summary}. Đang chờ bạn duyệt.",
            linkUrl: link);

        var email = new EmailContent(
            Subject: $"[Mingo Payroll] Có {kindLabel} mới cần duyệt (#{e.RequestId})",
            TextBody:
                $"Có {kindLabel} mới cần bạn duyệt:{Environment.NewLine}" +
                $"{e.Summary}{Environment.NewLine}{Environment.NewLine}" +
                $"Xem chi tiết: {link}");

        return SingleWithEmail(RecipientSpec.Users(e.ApproverUserIds), content, email);
    }

    internal static (string Title, string KindLabel, string Link) KindDescriptor(
        AttendanceRequestKind kind, int id) => kind switch
    {
        AttendanceRequestKind.Leave => ("Đơn nghỉ phép mới", "đơn nghỉ phép", $"/attendance/leave-requests/{id}"),
        AttendanceRequestKind.Overtime => ("Đơn OT mới", "đơn OT", $"/attendance/overtime-requests/{id}"),
        AttendanceRequestKind.LateEarly => ("Đơn đi muộn/về sớm mới", "đơn đi muộn/về sớm", $"/attendance/late-early-requests/{id}"),
        AttendanceRequestKind.Wfh => ("Đơn WFH mới", "đơn WFH", $"/attendance/wfh-requests/{id}"),
        AttendanceRequestKind.Adjustment => ("Đơn chỉnh công mới", "đơn chỉnh công", $"/attendance/adjustments/{id}"),
        AttendanceRequestKind.Dependent => ("Đơn đăng ký người phụ thuộc mới", "đơn NPT", $"/employees/dependent-requests/{id}"),
        _ => ("Đơn mới", "đơn", "/"),
    };
}
