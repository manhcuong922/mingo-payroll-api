namespace MPS.Domain.Common;

/// <summary>
/// Giờ Việt Nam (UTC+7) cho các module Task/Production và Compliance — lưu thẳng vào DB,
/// KHÔNG phải UTC. Không dùng cho Auth/Contract/Attendance/Payroll (các module đó vẫn UTC qua IClock).
/// </summary>
public static class VietnamClock
{
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
}
