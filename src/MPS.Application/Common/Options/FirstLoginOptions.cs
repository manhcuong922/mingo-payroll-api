namespace MPS.Application.Common.Options;

/// <summary>
/// Cấu hình flow đặt mật khẩu lần đầu (Hướng B — plan §2 N8):
/// khi cấp tài khoản mới cho NV, hệ thống KHÔNG gửi password thô mà gửi 1 link 1-lần
/// dẫn tới form đặt password. Link có thời hạn cấu hình ở đây.
///
/// Cùng cơ chế token (<see cref="MPS.Domain.Entities.PasswordResetToken"/>) và endpoint
/// đặt password (<c>POST /api/auth/reset-password</c>) như flow "quên mật khẩu" — chỉ khác
/// mail template và thời gian hết hạn (invitation dài hơn vì user không chủ động đợi mail).
/// </summary>
public sealed class FirstLoginOptions
{
    public const string SectionName = "FirstLogin";

    /// <summary>Thời hạn token đặt password lần đầu (giờ). Mặc định 72h để bao được cuối tuần.</summary>
    public int TokenLifetimeHours { get; init; } = 72;

    /// <summary>
    /// URL nền frontend (không có trailing slash). Mail sẽ nối path + <c>?token=...</c> vào đây.
    /// Nếu để trống, worker fallback về <see cref="MPS.Infrastructure.Email.SmtpOptions.LoginUrl"/>
    /// (có thể chỉ ra trang login mà không có link đặt password — user sẽ phải dùng "quên mật khẩu").
    /// </summary>
    public string? FrontendBaseUrl { get; init; }

    /// <summary>Path trên FE để mở form đặt password. Placeholder <c>{token}</c> sẽ được thay bằng raw token.</summary>
    public string LinkPath { get; init; } = "/first-login?token={token}";

    public TimeSpan TokenLifetime => TimeSpan.FromHours(TokenLifetimeHours <= 0 ? 72 : TokenLifetimeHours);
}
