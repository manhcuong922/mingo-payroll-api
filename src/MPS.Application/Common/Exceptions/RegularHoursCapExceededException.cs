using MPS.Application.Common.Interfaces;

namespace MPS.Application.Common.Exceptions;

/// <summary>
/// Thrown when regular working-hour caps are exceeded and the operation must be blocked.
/// </summary>
public sealed class RegularHoursCapExceededException : Exception
{
    public IReadOnlyList<DailyCapWarning> DailyViolations { get; }
    public IReadOnlyList<WeeklyCapWarning> WeeklyViolations { get; }

    public RegularHoursCapExceededException(
        IReadOnlyList<DailyCapWarning> dailyViolations,
        IReadOnlyList<WeeklyCapWarning> weeklyViolations)
        : base(BuildMessage(dailyViolations.Count, weeklyViolations.Count))
    {
        DailyViolations = dailyViolations;
        WeeklyViolations = weeklyViolations;
    }

    private static string BuildMessage(int dailyCount, int weeklyCount)
    {
        var total = dailyCount + weeklyCount;
        return $"Không thể phân ca: phát hiện {total} vi phạm giới hạn giờ làm việc bình thường (8h/ngày hoặc 48h/tuần).";
    }
}
