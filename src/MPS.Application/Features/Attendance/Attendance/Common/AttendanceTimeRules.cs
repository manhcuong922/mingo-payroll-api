namespace MPS.Application.Features.Attendance.Attendance.Common;

/// <summary>
/// Rule chống nhập giờ chấm công ở tương lai. Áp dụng vì hệ thống không dùng thiết bị
/// chấm công ngoại vi — toàn bộ giờ do DM/HR quan sát rồi nhập tay, nên một thời điểm
/// trong tương lai không thể là "đã quan sát thấy".
/// </summary>
public static class AttendanceTimeRules
{
    /// <summary>WorkDate không được vượt quá <paramref name="today"/> (thường là ngày hiện tại theo giờ VN).</summary>
    public static bool IsWorkDateInFuture(DateOnly workDate, DateOnly today) =>
        workDate > today;

    public static bool HasValidObservedRange(DateTime? checkInAt, DateTime? checkOutAt) =>
        checkInAt is null || checkOutAt is null || checkOutAt > checkInAt;

    /// <summary>
    /// Trả về error message nếu WorkDate vượt quá <paramref name="today"/>.
    /// Trả về null nếu hợp lệ.
    /// </summary>
    public static string? ValidateNotInFuture(
        DateOnly workDate,
        DateTime? checkInAt,
        DateTime? checkOutAt,
        DateOnly today)
    {
        if (IsWorkDateInFuture(workDate, today))
            return $"Ngày chấm công {workDate:dd/MM/yyyy} là ngày trong tương lai; chỉ được chấm công cho hôm nay hoặc trước đó.";

        return null;
    }
}
