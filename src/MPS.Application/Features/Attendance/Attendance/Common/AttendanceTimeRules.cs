namespace MPS.Application.Features.Attendance.Attendance.Common;

/// <summary>
/// Rule chống nhập giờ chấm công ở tương lai. Áp dụng vì hệ thống không dùng thiết bị
/// chấm công ngoại vi — toàn bộ giờ do DM/HR quan sát rồi nhập tay, nên một thời điểm
/// trong tương lai không thể là "đã quan sát thấy".
/// </summary>
public static class AttendanceTimeRules
{
    /// <summary>WorkDate không được vượt quá ngày hiện tại (UTC).</summary>
    public static bool IsWorkDateInFuture(DateOnly workDate, DateTime nowUtc) =>
        workDate > DateOnly.FromDateTime(nowUtc);

    public static bool HasValidObservedRange(DateTime? checkInAt, DateTime? checkOutAt) =>
        checkInAt is null || checkOutAt is null || checkOutAt > checkInAt;

    /// <summary>
    /// Trả về error message nếu WorkDate vượt quá now.
    /// Trả về null nếu hợp lệ.
    /// </summary>
    public static string? ValidateNotInFuture(
        DateOnly workDate,
        DateTime? checkInAt,
        DateTime? checkOutAt,
        DateTime nowUtc)
    {
        if (IsWorkDateInFuture(workDate, nowUtc))
            return $"WorkDate {workDate:yyyy-MM-dd} is in the future; attendance can only be recorded for past or current day.";

        return null;
    }
}
