namespace MPS.Application.Features.Attendance.OvertimeRequest.Common;

/// <summary>
/// Absolute-time overlap check (handles cross-midnight OT).
/// Dùng chung cho check trùng OT (create batch + update pending).
/// </summary>
internal static class OvertimeTimeOverlap
{
    public static bool Overlaps(
        DateOnly aDate,
        TimeOnly aStart,
        TimeOnly aEnd,
        DateOnly bDate,
        TimeOnly bStart,
        TimeOnly bEnd)
    {
        var (aAbsStart, aAbsEnd) = ToAbsoluteRange(aDate, aStart, aEnd);
        var (bAbsStart, bAbsEnd) = ToAbsoluteRange(bDate, bStart, bEnd);
        return aAbsStart < bAbsEnd && bAbsStart < aAbsEnd;
    }

    private static (long Start, long End) ToAbsoluteRange(DateOnly date, TimeOnly start, TimeOnly end)
    {
        var baseTicks = (long)date.DayNumber * TimeSpan.TicksPerDay;
        var absoluteStart = baseTicks + start.Ticks;
        var absoluteEnd = baseTicks + end.Ticks;
        if (absoluteEnd <= absoluteStart)
            absoluteEnd += TimeSpan.TicksPerDay;
        return (absoluteStart, absoluteEnd);
    }
}
