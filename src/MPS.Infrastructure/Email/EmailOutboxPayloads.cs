namespace MPS.Infrastructure.Email;

// Hợp đồng nội bộ giữa OutboxEmailSender (ghi) và EmailDispatchJob (đọc) — serialize vào
// EmailOutboxMessage.PayloadJson. Không rò ra ngoài Infrastructure.

internal sealed record PasswordResetPayload(string ResetToken, DateTime ExpiresAt);

internal sealed record AccountCredentialsPayload(string FullName, string Username, string PlainPassword);

internal sealed record FirstLoginInvitationPayload(
    string FullName,
    string Username,
    string InvitationUrl,
    DateTime ExpiresAt);

internal sealed record ContractActivationPayload(
    string FullName,
    string ContractNo,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string PdfFileName);

/// <summary>
/// Payload cho <see cref="MPS.Domain.Enums.EmailOutboxType.GenericNotification"/> — nội dung
/// email đã được template thông báo render sẵn (Subject + Text/Html). Worker gửi thẳng, không xử lý thêm.
/// </summary>
internal sealed record GenericNotificationPayload(
    string Subject,
    string TextBody,
    string? HtmlBody);
