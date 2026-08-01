using System.Net;

namespace MPS.Infrastructure.Email.Templates;

/// <summary>
/// Mail mời đặt mật khẩu lần đầu (Hướng B — plan §2 N8): username + link đặt password 1-lần.
/// KHÔNG chứa password thô. Link hết hạn sau <c>FirstLoginOptions.TokenLifetimeHours</c> giờ;
/// hết hạn thì NV dùng "Quên mật khẩu" để lấy link mới, hoặc HR bấm "resend".
/// </summary>
internal static class FirstLoginInvitationTemplate
{
    public const string Subject = "[Mingo Payroll] Thiết lập mật khẩu tài khoản";

    public static string BuildHtml(string fullName, string username, string invitationUrl, DateTime expiresAt)
    {
        var safeName = WebUtility.HtmlEncode(fullName);
        var safeUsername = WebUtility.HtmlEncode(username);
        var safeUrl = WebUtility.HtmlEncode(invitationUrl);
        var expiresLocal = expiresAt.ToLocalTime().ToString("HH:mm dd/MM/yyyy");

        return $$"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width,initial-scale=1" />
              <title>Thiết lập mật khẩu Mingo Payroll</title>
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
                          <p style="margin:0 0 12px;">Chào <strong>{{safeName}}</strong>,</p>
                          <p style="margin:0 0 16px;">
                            Phòng Nhân sự vừa tạo tài khoản đăng nhập cho bạn trên hệ thống Mingo Payroll.
                            Để bảo mật, chúng tôi <strong>KHÔNG gửi mật khẩu qua email</strong> —
                            vui lòng nhấn nút bên dưới để tự đặt mật khẩu của bạn.
                          </p>
                          <table role="presentation" cellpadding="0" cellspacing="0"
                                 style="background:#f9fafb;border:1px solid #e5e7eb;border-radius:6px;
                                        width:100%;margin:16px 0;">
                            <tr>
                              <td style="padding:14px 18px;">
                                <div style="color:#6b7280;font-size:12px;">Tên đăng nhập</div>
                                <div style="font-family:Consolas,'Courier New',monospace;font-size:15px;
                                            color:#111827;margin-top:4px;">{{safeUsername}}</div>
                              </td>
                            </tr>
                          </table>
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                            <tr>
                              <td align="center" style="padding:24px 0 8px;">
                                <a href="{{safeUrl}}"
                                   style="background:#1e3a8a;color:#ffffff;text-decoration:none;
                                          padding:12px 28px;border-radius:6px;font-weight:600;
                                          display:inline-block;font-family:Arial,sans-serif;">
                                  Đặt mật khẩu
                                </a>
                              </td>
                            </tr>
                          </table>
                          <p style="margin:16px 0 6px;color:#6b7280;font-size:12px;">
                            Nếu nút không hoạt động, sao chép link sau vào trình duyệt:<br />
                            <span style="color:#374151;word-break:break-all;">{{safeUrl}}</span>
                          </p>
                          <p style="margin:24px 0 6px;color:#b91c1c;font-weight:600;">⚠ Lưu ý</p>
                          <ul style="margin:0;padding-left:18px;color:#374151;">
                            <li>Link chỉ dùng được 1 lần và hết hạn lúc <strong>{{expiresLocal}}</strong>.</li>
                            <li>Nếu quá hạn, hãy dùng chức năng "Quên mật khẩu" trên trang đăng nhập để nhận link mới,
                                hoặc liên hệ Phòng Nhân sự.</li>
                            <li>Nếu bạn không yêu cầu tạo tài khoản, vui lòng bỏ qua email này.</li>
                          </ul>
                        </td>
                      </tr>
                      <tr>
                        <td style="background:#f9fafb;color:#6b7280;font-size:12px;
                                   padding:16px 28px;border-top:1px solid #e5e7eb;">
                          © Mingo Payroll · Phòng Nhân sự
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

    public static string BuildPlainText(string fullName, string username, string invitationUrl, DateTime expiresAt)
    {
        var expiresLocal = expiresAt.ToLocalTime().ToString("HH:mm dd/MM/yyyy");
        var lines = new List<string>
        {
            $"Chào {fullName},",
            string.Empty,
            "Phòng Nhân sự vừa tạo tài khoản đăng nhập cho bạn trên hệ thống Mingo Payroll.",
            "Để bảo mật, chúng tôi KHÔNG gửi mật khẩu qua email — vui lòng đặt mật khẩu tại link dưới đây:",
            string.Empty,
            $"  Tên đăng nhập: {username}",
            $"  Link đặt mật khẩu: {invitationUrl}",
            string.Empty,
            $"Link chỉ dùng được 1 lần và hết hạn lúc {expiresLocal}.",
            "Nếu quá hạn, dùng \"Quên mật khẩu\" trên trang đăng nhập để nhận link mới, hoặc liên hệ Phòng Nhân sự.",
            string.Empty,
            "— Mingo Payroll",
        };
        return string.Join(Environment.NewLine, lines);
    }
}
