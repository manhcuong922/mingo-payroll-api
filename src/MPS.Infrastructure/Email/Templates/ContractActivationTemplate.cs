using System.Globalization;
using System.Net;

namespace MPS.Infrastructure.Email.Templates;

internal static class ContractActivationTemplate
{
    public const string Subject = "[Mingo Payroll] Hợp đồng lao động đã được kích hoạt";

    public static string BuildHtml(
        string fullName,
        string contractNo,
        DateTime effectiveFrom,
        DateTime? effectiveTo,
        string? username,
        string? plainPassword,
        string? loginUrl)
    {
        var safeName = WebUtility.HtmlEncode(fullName);
        var safeContractNo = WebUtility.HtmlEncode(contractNo);
        var safeFrom = effectiveFrom.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var safeTo = effectiveTo.HasValue
            ? effectiveTo.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : "Không thời hạn";
        var safeLoginUrl = string.IsNullOrWhiteSpace(loginUrl) ? null : WebUtility.HtmlEncode(loginUrl);

        var credentialsBlock = (username is null || plainPassword is null)
            ? string.Empty
            : $$"""
              <p style="margin:0 0 8px;">Tài khoản đăng nhập hệ thống Mingo Payroll của bạn:</p>
              <table role="presentation" cellpadding="0" cellspacing="0"
                     style="background:#f9fafb;border:1px solid #e5e7eb;border-radius:6px;
                            width:100%;margin:8px 0 16px;">
                <tr>
                  <td style="padding:14px 18px;border-bottom:1px solid #e5e7eb;">
                    <div style="color:#6b7280;font-size:12px;">Username</div>
                    <div style="font-family:Consolas,'Courier New',monospace;font-size:15px;
                                color:#111827;margin-top:4px;">{{WebUtility.HtmlEncode(username)}}</div>
                  </td>
                </tr>
                <tr>
                  <td style="padding:14px 18px;">
                    <div style="color:#6b7280;font-size:12px;">Mật khẩu</div>
                    <div style="font-family:Consolas,'Courier New',monospace;font-size:15px;
                                color:#111827;margin-top:4px;">{{WebUtility.HtmlEncode(plainPassword)}}</div>
                  </td>
                </tr>
              </table>
              """;

        var ctaBlock = safeLoginUrl is null
            ? string.Empty
            : $$"""
              <tr>
                <td align="center" style="padding:8px 0 8px;">
                  <a href="{{safeLoginUrl}}"
                     style="background:#1e3a8a;color:#ffffff;text-decoration:none;
                            padding:12px 28px;border-radius:6px;font-weight:600;
                            display:inline-block;font-family:Arial,sans-serif;">
                    Đăng nhập ngay
                  </a>
                </td>
              </tr>
              """;

        return $$"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width,initial-scale=1" />
              <title>Hợp đồng đã kích hoạt</title>
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
                            Hợp đồng lao động của bạn đã được kích hoạt thành công. Bản hợp đồng có chữ ký
                            được đính kèm trong email này (định dạng PDF).
                          </p>
                          <table role="presentation" cellpadding="0" cellspacing="0"
                                 style="background:#f9fafb;border:1px solid #e5e7eb;border-radius:6px;
                                        width:100%;margin:8px 0 16px;">
                            <tr>
                              <td style="padding:14px 18px;border-bottom:1px solid #e5e7eb;">
                                <div style="color:#6b7280;font-size:12px;">Số hợp đồng</div>
                                <div style="font-size:15px;color:#111827;margin-top:4px;">{{safeContractNo}}</div>
                              </td>
                            </tr>
                            <tr>
                              <td style="padding:14px 18px;border-bottom:1px solid #e5e7eb;">
                                <div style="color:#6b7280;font-size:12px;">Hiệu lực từ</div>
                                <div style="font-size:15px;color:#111827;margin-top:4px;">{{safeFrom}}</div>
                              </td>
                            </tr>
                            <tr>
                              <td style="padding:14px 18px;">
                                <div style="color:#6b7280;font-size:12px;">Hiệu lực đến</div>
                                <div style="font-size:15px;color:#111827;margin-top:4px;">{{safeTo}}</div>
                              </td>
                            </tr>
                          </table>
                          {{credentialsBlock}}
                          {{ctaBlock}}
                          <p style="margin:24px 0 6px;color:#b91c1c;font-weight:600;">⚠ Lưu ý</p>
                          <ul style="margin:0;padding-left:18px;color:#374151;">
                            <li>Vui lòng lưu lại file PDF hợp đồng đính kèm để tham chiếu sau này.</li>
                            <li>Nếu email có thông tin đăng nhập, vui lòng đổi mật khẩu ngay sau lần đăng nhập đầu tiên.</li>
                            <li>Email này được gửi tự động, vui lòng không trả lời trực tiếp.</li>
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

    public static string BuildPlainText(
        string fullName,
        string contractNo,
        DateTime effectiveFrom,
        DateTime? effectiveTo,
        string? username,
        string? plainPassword,
        string? loginUrl)
    {
        var effectiveToText = effectiveTo.HasValue
            ? effectiveTo.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : "Không thời hạn";

        var lines = new List<string>
        {
            $"Chào {fullName},",
            string.Empty,
            "Hợp đồng lao động của bạn đã được kích hoạt thành công.",
            "Bản hợp đồng có chữ ký được đính kèm dưới dạng file PDF.",
            string.Empty,
            $"  Số hợp đồng: {contractNo}",
            $"  Hiệu lực từ: {effectiveFrom:dd/MM/yyyy}",
            $"  Hiệu lực đến: {effectiveToText}",
        };

        if (username is not null && plainPassword is not null)
        {
            lines.Add(string.Empty);
            lines.Add("Tài khoản đăng nhập hệ thống Mingo Payroll:");
            lines.Add($"  Username: {username}");
            lines.Add($"  Mật khẩu: {plainPassword}");
        }

        if (!string.IsNullOrWhiteSpace(loginUrl))
        {
            lines.Add(string.Empty);
            lines.Add($"Đăng nhập tại: {loginUrl}");
        }

        lines.Add(string.Empty);
        lines.Add("Lưu ý:");
        lines.Add(" - Vui lòng lưu lại file PDF hợp đồng đính kèm để tham chiếu sau này.");
        if (username is not null)
        {
            lines.Add(" - Đổi mật khẩu ngay sau lần đăng nhập đầu tiên.");
        }
        lines.Add(" - Email này được gửi tự động, vui lòng không trả lời trực tiếp.");
        lines.Add(string.Empty);
        lines.Add("— Mingo Payroll");

        return string.Join(Environment.NewLine, lines);
    }
}
