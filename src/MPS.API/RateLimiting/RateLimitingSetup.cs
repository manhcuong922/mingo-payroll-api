using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace MPS.API.RateLimiting;

/// <summary>Tên policy giới hạn tần suất — dùng chung giữa nơi khai báo và các controller.</summary>
public static class RateLimitPolicies
{
    public const string Login = "auth-login";
    public const string Refresh = "auth-refresh";
    public const string ContractUpload = "contract-upload";
    public const string Ocr = "ocr-citizen-id";
}

public static class RateLimitingSetup
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Khai báo toàn bộ hạn mức tần suất của API theo cấu hình <see cref="RateLimitingOptions"/>.
    ///
    /// Nguyên tắc phân vùng (partition):
    ///  - Đăng nhập: theo TÀI KHOẢN (username + IP) để chặn dò mật khẩu, kèm một trần theo IP
    ///    (<see cref="RateLimiterOptions.GlobalLimiter"/>) để chặn dò trải rộng nhiều tài khoản.
    ///  - Tải file hợp đồng / OCR: theo NGƯỜI DÙNG đã đăng nhập (fallback IP khi ẩn danh).
    ///    Muốn phân vùng theo người dùng thì <c>UseRateLimiter()</c> phải chạy SAU
    ///    <c>UseAuthentication()</c>, nếu không claim rỗng và mọi người dùng lại dùng chung một rổ IP.
    /// </summary>
    public static IServiceCollection AddMpsRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));
        var limits = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
            ?? new RateLimitingOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRetryAfterResponseAsync;

            // Trần theo IP cho nhóm đăng nhập. Đặt ở GlobalLimiter vì một endpoint chỉ gắn được
            // một policy, mà ở đây cần HAI lớp: theo tài khoản (bên dưới) và theo IP (ở đây).
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                IsLoginEndpoint(context)
                    ? FixedWindow($"login-ip:{ClientIp(context)}", limits.LoginPerIpPerMinute)
                    : RateLimitPartition.GetNoLimiter<string>("khong-gioi-han"));

            options.AddPolicy(RateLimitPolicies.Login, context =>
                FixedWindow(LoginPartitionKey(context), limits.LoginPerAccountPerMinute));

            options.AddPolicy(RateLimitPolicies.Refresh, context =>
                FixedWindow($"refresh-ip:{ClientIp(context)}", limits.RefreshPerIpPerMinute));

            options.AddPolicy(RateLimitPolicies.ContractUpload, context =>
                FixedWindow($"upload:{UserKey(context)}", limits.ContractUploadPerUserPerMinute));

            options.AddPolicy(RateLimitPolicies.Ocr, context =>
                FixedWindow($"ocr:{UserKey(context)}", limits.OcrPerUserPerMinute));
        });

        return services;
    }

    private static RateLimitPartition<string> FixedWindow(string partitionKey, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = Window,
                QueueLimit = 0,
            });

    /// <summary>
    /// Khoá phân vùng cho đăng nhập: ưu tiên (tài khoản + IP). Khi không đọc được username
    /// (quên mật khẩu / đặt lại mật khẩu / body hỏng) thì lùi về theo IP.
    /// </summary>
    private static string LoginPartitionKey(HttpContext context)
    {
        var ip = ClientIp(context);
        return context.Items.TryGetValue(LoginIdentityMiddleware.UsernameItemKey, out var raw)
            && raw is string username && username.Length > 0
                ? $"login-acct:{ip}|{username}"
                : $"login-noacct:{ip}";
    }

    private static bool IsLoginEndpoint(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            == RateLimitPolicies.Login;

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

    private static string UserKey(HttpContext context) =>
        context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? $"ip:{ClientIp(context)}";

    /// <summary>
    /// Nghiệp vụ (§13 + §1): khi chặn theo hạn mức phải **báo rõ thời gian chờ**, không được trả
    /// 429 rỗng. Trước đây body trống và KHÔNG có header <c>Retry-After</c> ⇒ người dùng chỉ thấy
    /// thao tác "im lặng không có gì xảy ra" và không biết chờ bao lâu; UI cũng không hiển thị được gì.
    /// </summary>
    private static async ValueTask WriteRetryAfterResponseAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : (int)Window.TotalSeconds;

        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        response.ContentType = "application/json";
        await response.WriteAsync(
            JsonSerializer.Serialize(new
            {
                error = $"Bạn thao tác quá nhanh. Vui lòng thử lại sau {retryAfterSeconds} giây.",
                retryAfterSeconds,
            }),
            cancellationToken);
    }
}
