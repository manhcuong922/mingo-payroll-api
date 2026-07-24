using System.Text;
using System.Text.Json;

namespace MPS.API.RateLimiting;

/// <summary>
/// Đọc trước <c>username</c> trong body của request đăng nhập và ghi vào <see cref="HttpContext.Items"/>
/// để bộ giới hạn tần suất phân vùng theo TÀI KHOẢN thay vì gộp chung cả IP.
///
/// Vì sao cần: hạn mức đăng nhập trước đây tính theo IP, mà cả công ty thường ra Internet qua một IP
/// NAT duy nhất ⇒ người thứ 6 đăng nhập đầu ca bị chặn dù nhập đúng mật khẩu. Bộ đếm phải bám vào
/// tài khoản đang bị dò, không bám vào đường truyền chung.
///
/// Body được bật buffering và tua lại vị trí 0 nên model binding phía sau vẫn đọc bình thường.
/// </summary>
public sealed class LoginIdentityMiddleware
{
    /// <summary>Khoá trong HttpContext.Items chứa username (đã chuẩn hoá thường) của request đăng nhập.</summary>
    public const string UsernameItemKey = "mps.login-username";

    private const string LoginPath = "/api/auth/login";
    private const int MaxBodyBytes = 8 * 1024;

    private readonly RequestDelegate _next;

    public LoginIdentityMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsLoginRequest(context))
        {
            await CaptureUsernameAsync(context);
        }

        await _next(context);
    }

    private static bool IsLoginRequest(HttpContext context) =>
        HttpMethods.IsPost(context.Request.Method)
        && context.Request.Path.StartsWithSegments(LoginPath, StringComparison.OrdinalIgnoreCase)
        && context.Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true
        && context.Request.ContentLength is null or <= MaxBodyBytes;

    private static async Task CaptureUsernameAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        try
        {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync(context.RequestAborted);
            if (string.IsNullOrWhiteSpace(body)) return;

            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, "username", StringComparison.OrdinalIgnoreCase)) continue;
                if (property.Value.ValueKind != JsonValueKind.String) continue;

                var username = property.Value.GetString()?.Trim();
                if (!string.IsNullOrEmpty(username))
                {
                    context.Items[UsernameItemKey] = username.ToLowerInvariant();
                }

                return;
            }
        }
        catch (JsonException)
        {
            // Body không phải JSON hợp lệ — cứ để model binding phía sau trả 400 như bình thường.
        }
        finally
        {
            context.Request.Body.Position = 0;
        }
    }
}
