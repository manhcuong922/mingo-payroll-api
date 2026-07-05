using System.Globalization;
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
        : base(BuildMessage(dailyViolations, weeklyViolations))
    {
        DailyViolations = dailyViolations;
        WeeklyViolations = weeklyViolations;
    }

    private static string BuildMessage(
        IReadOnlyList<DailyCapWarning> dailyViolations,
        IReadOnlyList<WeeklyCapWarning> weeklyViolations)
    {
        var parts = new List<string>();

        if (dailyViolations.Count > 0)
        {
            var dailyDetails = dailyViolations
                .OrderBy(v => v.WorkDate)
                .ThenBy(v => v.EmployeeInformationId)
                .Select(v =>
                    $"NV #{v.EmployeeInformationId} ngày {v.WorkDate:yyyy-MM-dd}: {FormatHours(v.TotalHoursAfter)}/{FormatHours(v.CapHours)}h")
                .ToList();

            parts.Add($"Vượt 8h/ngày: {string.Join("; ", dailyDetails)}");
        }

        if (weeklyViolations.Count > 0)
        {
            var weeklyDetails = weeklyViolations
                .OrderBy(v => v.IsoYear)
                .ThenBy(v => v.IsoWeek)
                .ThenBy(v => v.EmployeeInformationId)
                .Select(v =>
                    $"NV #{v.EmployeeInformationId} tuần {v.IsoYear}-W{v.IsoWeek:00} ({v.WeekStart:yyyy-MM-dd} đến {v.WeekEnd:yyyy-MM-dd}): {FormatHours(v.TotalHoursAfter)}/{FormatHours(v.CapHours)}h")
                .ToList();

            parts.Add($"Vượt 48h/tuần: {string.Join("; ", weeklyDetails)}");
        }

        return $"Không thể phân ca: {string.Join(". ", parts)}.";
    }

    private static string FormatHours(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
