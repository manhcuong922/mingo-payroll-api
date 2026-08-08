namespace MPS.Application.Common.Options;

/// <summary>
/// Cấu hình flow "quên mật khẩu": user nhập email → nhận mail chứa link 1-lần dẫn tới form
/// đặt lại mật khẩu trên FE.
///
/// Cùng cơ chế token (<see cref="MPS.Domain.Entities.PasswordResetToken"/>) và endpoint
/// (<c>POST /api/auth/reset-password</c>) như flow đặt mật khẩu lần đầu
/// (<see cref="FirstLoginOptions"/>) — chỉ khác mail template và thời hạn ngắn hơn
/// (1h vs 72h, vì user đang chủ động đợi mail).
/// </summary>
public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    /// <summary>Thời hạn token đặt lại mật khẩu (giờ). Mặc định 1h — user đang chủ động đợi mail.</summary>
    public int TokenLifetimeHours { get; init; } = 1;

    /// <summary>
    /// URL nền frontend (không có trailing slash), VD <c>https://operix.fun</c>. Mail nối
    /// <see cref="LinkPath"/> vào đây để tạo link bấm được. Để trống thì mail chỉ có path —
    /// user phải tự ghép domain, nên PHẢI cấu hình ở môi trường thật.
    /// </summary>
    public string? FrontendBaseUrl { get; init; }

    /// <summary>Path trên FE mở form đặt lại mật khẩu. Placeholder <c>{token}</c> thay bằng raw token.</summary>
    public string LinkPath { get; init; } = "/reset-password?token={token}";

    public TimeSpan TokenLifetime => TimeSpan.FromHours(TokenLifetimeHours <= 0 ? 1 : TokenLifetimeHours);
}
