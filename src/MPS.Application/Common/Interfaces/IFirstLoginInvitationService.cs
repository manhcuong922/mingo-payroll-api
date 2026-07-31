namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Cấp mời đặt mật khẩu lần đầu cho tài khoản mới (Hướng B — plan §2 N8).
/// Gọi ngay sau khi provisioner tạo <c>User</c>; và cả khi HR bấm "gửi lại" trên UI quản lý.
///
/// Cơ chế: sinh 1 <c>PasswordResetToken</c> hạn <c>FirstLoginOptions.TokenLifetimeHours</c> giờ,
/// invalidate mọi token active cũ của user, gửi mail chứa link đặt password (worker qua outbox).
/// Token dùng CHUNG với flow "quên mật khẩu" — khi user submit form đặt password, gọi
/// <c>POST /api/auth/reset-password</c> có sẵn.
/// </summary>
public interface IFirstLoginInvitationService
{
    /// <summary>Cấp mời cho user vừa tạo (hoặc HR resend). Trả về thời điểm token hết hạn (UTC).</summary>
    Task<DateTime> IssueAsync(int userId, CancellationToken cancellationToken = default);
}
