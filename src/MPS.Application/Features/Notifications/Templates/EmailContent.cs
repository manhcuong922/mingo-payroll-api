namespace MPS.Application.Features.Notifications.Templates;

/// <summary>
/// Nội dung email đi kèm một thông báo (Phase 1 mở kênh Email).
/// Template tuỳ chọn thêm field này vào <see cref="NotificationPlanItem"/> để yêu cầu gửi email.
///
/// <see cref="HtmlBody"/> tuỳ chọn; <see cref="TextBody"/> BẮT BUỘC để đảm bảo mọi client email
/// (kể cả text-only) đọc được. Nếu template chỉ có text, để HtmlBody = null — worker sẽ tự
/// gửi text/plain.
/// </summary>
public sealed record EmailContent(
    string Subject,
    string TextBody,
    string? HtmlBody = null);
