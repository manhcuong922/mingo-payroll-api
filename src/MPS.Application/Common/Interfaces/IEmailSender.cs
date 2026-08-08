namespace MPS.Application.Common.Interfaces;

public interface IEmailSender
{
    /// <summary>
    /// Gửi mail "quên mật khẩu". <paramref name="resetUrl"/> là link 1-lần đã ghép sẵn từ
    /// PasswordResetOptions.FrontendBaseUrl + LinkPath — mail hiển thị nút bấm dẫn thẳng tới
    /// form đặt lại mật khẩu. <paramref name="resetToken"/> giữ lại để template fallback hiển thị
    /// mã thô khi FrontendBaseUrl chưa cấu hình (và cho outbox cũ chưa có URL).
    /// </summary>
    Task SendPasswordResetAsync(string toEmail, string resetToken, string resetUrl, DateTime expiresAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// [DEPRECATED] Gửi mail chứa username + password thô. KHÔNG dùng cho tài khoản mới —
    /// hướng B của plan yêu cầu <see cref="SendFirstLoginInvitationAsync"/> (link đặt password 1-lần).
    /// Giữ signature để dữ liệu outbox cũ vẫn dispatch được sau khi upgrade.
    /// </summary>
    [Obsolete("Dùng SendFirstLoginInvitationAsync cho tài khoản mới. Xem plan §2 N8.")]
    Task SendAccountCredentialsAsync(
        string toEmail,
        string fullName,
        string username,
        string plainPassword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi mail mời đặt mật khẩu lần đầu (Hướng B). Mail chứa username + link 1-lần dẫn tới form
    /// đặt password; KHÔNG chứa password thô. <paramref name="invitationUrl"/> đã được ghép sẵn
    /// từ FirstLoginOptions.FrontendBaseUrl + LinkPath.
    /// </summary>
    Task SendFirstLoginInvitationAsync(
        string toEmail,
        string fullName,
        string username,
        string invitationUrl,
        DateTime expiresAt,
        CancellationToken cancellationToken = default);

    // PDF không truyền qua stream nữa (để message-friendly cho outbox). Chỉ truyền vị trí
    // trên MinIO; bên gửi thật (IRealEmailSender) sẽ tự tải file khi cần.
    Task SendContractActivationAsync(
        string toEmail,
        string fullName,
        ContractActivationEmailPayload payload,
        string storageBucket,
        string storageObjectKey,
        string pdfFileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Email chung phát sinh từ NotificationService (Phase 1 kênh Email). Subject/text/html đã render sẵn
    /// ở template thông báo — worker gửi thẳng, không xử lý thêm.
    /// </summary>
    Task SendGenericNotificationAsync(
        string toEmail,
        string subject,
        string textBody,
        string? htmlBody,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Cùng hợp đồng với <see cref="IEmailSender"/> nhưng đại diện cho bên GỬI THẬT (SMTP/logging).
/// Handler inject <see cref="IEmailSender"/> (ghi outbox); worker inject <see cref="IRealEmailSender"/> (gửi đi).
/// </summary>
public interface IRealEmailSender : IEmailSender
{
}

/// <summary>
/// Payload email kích hoạt hợp đồng — CHỈ chứa thông tin HĐ + PDF. Credentials không còn nằm ở đây
/// (plan §2 N7): mail tài khoản tách riêng qua <see cref="IFirstLoginInvitationService"/>.
/// </summary>
public sealed record ContractActivationEmailPayload(
    string ContractNo,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo);
