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

    /// <summary>
    /// Trả về error message nếu CheckInTime/CheckOutTime gắn với WorkDate vượt quá now.
    /// Trả về null nếu hợp lệ.
    /// </summary>
    public static string? ValidateNotInFuture(
        DateOnly workDate,
        TimeOnly? checkInTime,
        TimeOnly? checkOutTime,
        DateTime nowUtc)
    {
        if (IsWorkDateInFuture(workDate, nowUtc))
            return $"WorkDate {workDate:yyyy-MM-dd} is in the future; attendance can only be recorded for past or current day.";

        if (checkInTime is { } ci && workDate.ToDateTime(ci) > nowUtc)
            return $"CheckInTime {ci:HH:mm} on {workDate:yyyy-MM-dd} is in the future.";

        if (checkOutTime is { } co && workDate.ToDateTime(co) > nowUtc)
            return $"CheckOutTime {co:HH:mm} on {workDate:yyyy-MM-dd} is in the future.";

        return null;
    }
}
