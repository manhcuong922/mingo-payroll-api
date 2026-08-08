using System.Net;

namespace MPS.Infrastructure.Email.Templates;

/// <summary>
/// Mail "quên mật khẩu": link 1-lần dẫn tới form đặt lại mật khẩu trên FE.
/// Link hết hạn sau <c>PasswordResetOptions.TokenLifetimeHours</c> giờ.
///
/// Khi <c>resetUrl</c> rỗng (chưa cấu hình FrontendBaseUrl, hoặc message outbox cũ ghi trước khi
/// có URL) thì rơi về bản chỉ hiển thị mã thô — vẫn gửi được, không chặn user.
/// </summary>
internal static class PasswordResetTemplate
{
    public const string Subject = "[Mingo Payroll] Yêu cầu đặt lại mật khẩu";

    public static string BuildHtml(string resetToken, string resetUrl, DateTime expiresAt)
    {
        var expiresLocal = expiresAt.ToLocalTime().ToString("HH:mm dd/MM/yyyy");
        var body = string.IsNullOrWhiteSpace(resetUrl)
            ? BuildTokenOnlyBlock(resetToken)
            : BuildLinkBlock(resetUrl);

        return $$"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width,initial-scale=1" />
              <title>Đặt lại mật khẩu Mingo Payroll</title>
            </head>
            <body style="margin:0;padding:0;background:#f3f4f6;font-family:Arial,Helvetica,sans-serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0"
                     style="background:#f3f4f6;padding:32px 0;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="560" cellpadding="0" cellspacing="0"
                           style="background:#ffffff;border-radius:8px;overflow:hidden;
                                  box-shadow:0 1px 3px rgba(0,0,0,0.08);">
                      <tr>
                        <td style="background:#1e3a8a;color:#ffffff;padding:20px 28px;font-size:18px;font-weight:700;">
                          Mingo Payroll
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:28px;color:#111827;font-size:14px;line-height:1.6;">
                          <p style="margin:0 0 16px;">
                            Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản Mingo Payroll của bạn.
                          </p>
                          {{body}}
                          <p style="margin:24px 0 6px;color:#b91c1c;font-weight:600;">⚠ Lưu ý</p>
                          <ul style="margin:0;padding-left:18px;color:#374151;">
                            <li>Link chỉ dùng được 1 lần và hết hạn lúc <strong>{{expiresLocal}}</strong>.</li>
                            <li>Sau khi đổi mật khẩu, mọi phiên đăng nhập cũ sẽ bị đăng xuất.</li>
                            <li>Nếu bạn không yêu cầu, vui lòng bỏ qua email này — mật khẩu hiện tại vẫn an toàn.</li>
                          </ul>
                        </td>
                      </tr>
                      <tr>
                        <td style="background:#f9fafb;color:#6b7280;font-size:12px;
                                   padding:16px 28px;border-top:1px solid #e5e7eb;">
                          © Mingo Payroll · Email tự động, vui lòng không trả lời.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string BuildLinkBlock(string resetUrl)
    {
        var safeUrl = WebUtility.HtmlEncode(resetUrl);

        return $$"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                            <tr>
                              <td align="center" style="padding:16px 0 8px;">
                                <a href="{{safeUrl}}"
                                   style="background:#1e3a8a;color:#ffffff;text-decoration:none;
                                          padding:12px 28px;border-radius:6px;font-weight:600;
                                          display:inline-block;font-family:Arial,sans-serif;">
                                  Đặt lại mật khẩu
                                </a>
                              </td>
                            </tr>
                          </table>
                          <p style="margin:16px 0 6px;color:#6b7280;font-size:12px;">
                            Nếu nút không hoạt động, sao chép link sau vào trình duyệt:<br />
                            <span style="color:#374151;word-break:break-all;">{{safeUrl}}</span>
                          </p>
            """;
    }

    private static string BuildTokenOnlyBlock(string resetToken)
    {
        var safeToken = WebUtility.HtmlEncode(resetToken);

        return $$"""
            <p style="margin:0 0 8px;">Nhập mã sau vào màn hình đặt lại mật khẩu:</p>
                          <table role="presentation" cellpadding="0" cellspacing="0"
                                 style="background:#f9fafb;border:1px solid #e5e7eb;border-radius:6px;
                                        width:100%;margin:8px 0 16px;">
                            <tr>
                              <td style="padding:14px 18px;">
                                <div style="color:#6b7280;font-size:12px;">Mã đặt lại mật khẩu</div>
                                <div style="font-family:Consolas,'Courier New',monospace;font-size:14px;
                                            color:#111827;margin-top:4px;word-break:break-all;">{{safeToken}}</div>
                              </td>
                            </tr>
                          </table>
            """;
    }

    public static string BuildPlainText(string resetToken, string resetUrl, DateTime expiresAt)
    {
        var expiresLocal = expiresAt.ToLocalTime().ToString("HH:mm dd/MM/yyyy");
        var lines = new List<string>
        {
            "Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản Mingo Payroll của bạn.",
            string.Empty,
        };

        if (string.IsNullOrWhiteSpace(resetUrl))
            lines.Add($"  Mã đặt lại mật khẩu: {resetToken}");
        else
            lines.Add($"  Link đặt lại mật khẩu: {resetUrl}");

        lines.AddRange(new[]
        {
            string.Empty,
            $"Link chỉ dùng được 1 lần và hết hạn lúc {expiresLocal}.",
            "Sau khi đổi mật khẩu, mọi phiên đăng nhập cũ sẽ bị đăng xuất.",
            "Nếu bạn không yêu cầu, vui lòng bỏ qua email này — mật khẩu hiện tại vẫn an toàn.",
            string.Empty,
            "— Mingo Payroll",
        });

        return string.Join(Environment.NewLine, lines);
    }
}
