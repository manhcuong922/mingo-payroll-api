namespace MPS.Application.Common.Services;

/// <summary>
/// Ghép link FE cho các mail chứa token 1-lần (đặt mật khẩu lần đầu, quên mật khẩu).
/// Dùng chung để 2 flow không trôi lệch cách escape token / xử lý trailing slash.
/// </summary>
internal static class FrontendLinkBuilder
{
    /// <summary>
    /// Ghép <paramref name="baseUrl"/> + <paramref name="linkPath"/> (đã thay <c>{token}</c>).
    /// Nếu <paramref name="baseUrl"/> trống thì trả về path — vẫn có ý nghĩa để dev/HR nhìn ra
    /// là thiếu cấu hình domain, thay vì trả về link rỗng.
    /// </summary>
    public static string Build(string? baseUrl, string linkPath, string rawToken)
    {
        var trimmedBase = baseUrl?.TrimEnd('/');
        var path = linkPath.Replace("{token}", Uri.EscapeDataString(rawToken));
        if (!path.StartsWith('/')) path = "/" + path;

        return string.IsNullOrWhiteSpace(trimmedBase) ? path : trimmedBase + path;
    }
}
