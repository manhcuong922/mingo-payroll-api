namespace MPS.Application.Features.Attendance.ShiftTemplate.Common;

internal static class ShiftTimeCalculator
{
    private const int MinutesPerDay = 24 * 60;

    public static bool HasValidRange(TimeOnly start, TimeOnly end) => end != start;

    public static decimal NetHours(
        TimeOnly start,
        TimeOnly end,
        TimeOnly? breakStart,
        TimeOnly? breakEnd)
    {
        var (shiftStart, shiftEnd) = NormalizeRange(start, end);
        var netMinutes = shiftEnd - shiftStart;

        if (breakStart.HasValue && breakEnd.HasValue)
        {
            var (bs, be) = NormalizeBreakRange(shiftStart, breakStart.Value, breakEnd.Value);
            netMinutes -= be - bs;
        }

        return Math.Round((decimal)netMinutes / 60m, 2);
    }

    public static bool HasValidBreakRange(
        TimeOnly start,
        TimeOnly end,
        TimeOnly? breakStart,
        TimeOnly? breakEnd)
    {
        if (!breakStart.HasValue && !breakEnd.HasValue)
            return true;

        if (!breakStart.HasValue || !breakEnd.HasValue || !HasValidRange(start, end))
            return false;

        var (shiftStart, shiftEnd) = NormalizeRange(start, end);
        var (bs, be) = NormalizeBreakRange(shiftStart, breakStart.Value, breakEnd.Value);

        return bs >= shiftStart && bs < shiftEnd && be > shiftStart && be <= shiftEnd && be > bs;
    }

    public static bool Overlaps(
        DateOnly leftDate,
        TimeOnly leftStart,
        TimeOnly leftEnd,
        DateOnly rightDate,
        TimeOnly rightStart,
        TimeOnly rightEnd)
    {
        var (leftAbsoluteStart, leftAbsoluteEnd) = ToAbsoluteRange(leftDate, leftStart, leftEnd);
        var (rightAbsoluteStart, rightAbsoluteEnd) = ToAbsoluteRange(rightDate, rightStart, rightEnd);

        return leftAbsoluteStart < rightAbsoluteEnd && rightAbsoluteStart < leftAbsoluteEnd;
    }

    private static (int Start, int End) NormalizeRange(TimeOnly start, TimeOnly end)
    {
        var s = ToMinutes(start);
        var e = ToMinutes(end);
        if (e <= s)
        {
            e += MinutesPerDay;
        }

        return (s, e);
    }

    private static (int Start, int End) NormalizeBreakRange(
        int shiftStart,
        TimeOnly breakStart,
        TimeOnly breakEnd)
    {
        var bs = ToMinutes(breakStart);
        var be = ToMinutes(breakEnd);

        if (bs < shiftStart) bs += MinutesPerDay;
        if (be < shiftStart) be += MinutesPerDay;
        if (be <= bs) be += MinutesPerDay;

        return (bs, be);
    }

    private static (long Start, long End) ToAbsoluteRange(
        DateOnly date,
        TimeOnly start,
        TimeOnly end)
    {
        var baseMinute = (long)date.DayNumber * MinutesPerDay;
        var s = baseMinute + ToMinutes(start);
        var e = baseMinute + ToMinutes(end);
        if (e <= s)
        {
            e += MinutesPerDay;
        }

        return (s, e);
    }

    private static int ToMinutes(TimeOnly time) => time.Hour * 60 + time.Minute;
}
