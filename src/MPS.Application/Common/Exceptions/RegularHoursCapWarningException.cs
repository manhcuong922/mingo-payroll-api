using MPS.Application.Common.Interfaces;

namespace MPS.Application.Common.Exceptions;

/// <summary>
/// Thrown when regular working hours exceed daily or weekly caps and caller has not acknowledged.
/// </summary>
public sealed class RegularHoursCapWarningException : Exception
{
    public IReadOnlyList<DailyCapWarning> DailyWarnings { get; }
    public IReadOnlyList<WeeklyCapWarning> WeeklyWarnings { get; }

    public RegularHoursCapWarningException(
        IReadOnlyList<DailyCapWarning> dailyWarnings,
        IReadOnlyList<WeeklyCapWarning> weeklyWarnings)
        : base(BuildMessage(dailyWarnings.Count, weeklyWarnings.Count))
    {
        DailyWarnings = dailyWarnings;
        WeeklyWarnings = weeklyWarnings;
    }

    private static string BuildMessage(int dailyCount, int weeklyCount)
    {
        var total = dailyCount + weeklyCount;
        return $"Phát hiện {total} cảnh báo vượt cap giờ làm việc bình thường (Điều 105 BLLĐ). Xác nhận để tiếp tục.";
    }
}
