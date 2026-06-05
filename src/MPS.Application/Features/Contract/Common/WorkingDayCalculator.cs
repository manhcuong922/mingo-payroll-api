namespace MPS.Application.Features.Contract.Common;

/// <summary>
/// Working-day helper for Điều 33 BLLĐ 2019 (phụ lục) — "EffectiveFrom ≥ NoticeSentAt + 3 working days".
/// Mặc định coi T2–T6 là ngày làm việc; T7, CN nghỉ. Chưa nạp lịch nghỉ lễ VN — phase sau có thể
/// nâng cấp khi cần (plan §8.2 ghi rõ).
/// </summary>
public static class WorkingDayCalculator
{
    /// <summary>
    /// Đếm số ngày làm việc (T2–T6) thuần trong khoảng nửa-mở [from, to).
    /// Trả 0 nếu from ≥ to. Dùng date-only so sánh để bỏ qua giờ.
    /// </summary>
    public static int CountWorkingDaysBetween(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date;
        if (end <= start) return 0;

        var totalDays = (int)(end - start).TotalDays;
        var count = 0;
        for (var i = 0; i < totalDays; i++)
        {
            var day = start.AddDays(i).DayOfWeek;
            if (day != DayOfWeek.Saturday && day != DayOfWeek.Sunday)
                count++;
        }
        return count;
    }

    /// <summary>
    /// True nếu khoảng cách giữa <paramref name="noticeSentAt"/> và <paramref name="effectiveFrom"/>
    /// đạt tối thiểu <paramref name="minWorkingDays"/> ngày làm việc.
    /// </summary>
    public static bool HasMinimumWorkingDayGap(DateTime noticeSentAt, DateTime effectiveFrom, int minWorkingDays) =>
        CountWorkingDaysBetween(noticeSentAt, effectiveFrom) >= minWorkingDays;
}
