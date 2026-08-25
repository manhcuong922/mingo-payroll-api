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

    /// <summary>
    /// Ngày LỊCH theo giờ VN của một mốc UTC. Dùng cho các module chạy theo <c>IClock</c> (UTC) nhưng
    /// cần so với dữ liệu ghi theo ngày làm việc — vd chấm công. Không có nó thì từ 00:00 đến 07:00
    /// giờ VN, <c>DateOnly.FromDateTime(utcNow)</c> trả về ngày HÔM QUA.
    /// </summary>
    public static DateOnly ToVietnamDate(DateTime utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, VietnamTimeZone));
}
