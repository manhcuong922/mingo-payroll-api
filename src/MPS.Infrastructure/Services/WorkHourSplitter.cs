using MPS.Application.Common.Interfaces;

namespace MPS.Infrastructure.Services;

/// <summary>
/// Pure function tách giờ làm thành (Day, Night) theo khung giờ đêm (mặc định
/// 22h–6h theo Điều 106 BLLĐ).
///
/// Algorithm:
///   1. Normalize tất cả timestamp về phút-từ-CheckIn-day-00:00. Nếu CheckOut
///      ≤ CheckIn thì shift cross-midnight → CheckOut += 1440.
///   2. Trừ break (break có thể wrap nếu rơi sau midnight).
///   3. Build night windows trong range cover được shift:
///        - Hôm qua: [NightStart − 1440, NightEnd] (vd [−120, 360] với 22h–6h)
///        - Hôm nay: [NightStart, NightEnd + 1440 nếu wrap]
///   4. Cho mỗi segment shift sau khi trừ break, tính tổng giao với night windows.
///   5. DayMinutes = TotalMinutes − NightMinutes.
/// </summary>
public sealed class WorkHourSplitter : IWorkHourSplitter
{
    public WorkHourBreakdown Split(WorkHourSplitInput input)
    {
        // 1. Convert to minutes-from-checkIn-day-00:00.
        var shiftStart = ToMinutes(input.CheckIn);
        var shiftEnd = ToMinutes(input.CheckOut);
        if (shiftEnd <= shiftStart) shiftEnd += 1440;     // cross-midnight

        var segments = BuildSegmentsAfterBreak(shiftStart, shiftEnd,
            input.BreakStart, input.BreakEnd);

        var nightWindows = BuildNightWindows(input.NightStart, input.NightEnd);

        var totalMin = 0;
        var nightMin = 0;
        foreach (var (segStart, segEnd) in segments)
        {
            totalMin += segEnd - segStart;
            foreach (var (winStart, winEnd) in nightWindows)
            {
                var overlapStart = Math.Max(segStart, winStart);
                var overlapEnd = Math.Min(segEnd, winEnd);
                if (overlapEnd > overlapStart)
                    nightMin += overlapEnd - overlapStart;
            }
        }

        var dayMin = totalMin - nightMin;

        return new WorkHourBreakdown(
            Math.Round((decimal)dayMin / 60m, 2),
            Math.Round((decimal)nightMin / 60m, 2));
    }

    public WorkHourBreakdown Split(WorkHourDateTimeSplitInput input)
    {
        if (input.CheckOutAt <= input.CheckInAt)
            return WorkHourBreakdown.Zero;

        var baseDate = input.CheckInAt.Date;
        var shiftStart = ToMinutesFromBase(input.CheckInAt, baseDate);
        var shiftEnd = ToMinutesFromBase(input.CheckOutAt, baseDate);

        var segments = BuildSegmentsAfterBreak(shiftStart, shiftEnd,
            input.BreakStart, input.BreakEnd);

        var nightWindows = BuildNightWindows(input.NightStart, input.NightEnd);

        var totalMin = 0;
        var nightMin = 0;
        foreach (var (segStart, segEnd) in segments)
        {
            totalMin += segEnd - segStart;
            foreach (var (winStart, winEnd) in nightWindows)
            {
                var overlapStart = Math.Max(segStart, winStart);
                var overlapEnd = Math.Min(segEnd, winEnd);
                if (overlapEnd > overlapStart)
                    nightMin += overlapEnd - overlapStart;
            }
        }

        var dayMin = totalMin - nightMin;

        return new WorkHourBreakdown(
            Math.Round((decimal)dayMin / 60m, 2),
            Math.Round((decimal)nightMin / 60m, 2));
    }

    private static List<(int Start, int End)> BuildSegmentsAfterBreak(
        int shiftStart, int shiftEnd,
        TimeOnly? breakStart, TimeOnly? breakEnd)
    {
        if (breakStart is null || breakEnd is null)
            return shiftEnd > shiftStart ? new() { (shiftStart, shiftEnd) } : new();

        var bs = ToMinutes(breakStart.Value);
        var be = ToMinutes(breakEnd.Value);

        // Nếu break trước shiftStart trên trục phút, giả định break thuộc next day.
        if (bs < shiftStart) bs += 1440;
        if (be < shiftStart) be += 1440;
        if (be <= bs) be += 1440;

        // Break có thực sự nằm trong shift?
        if (bs >= shiftEnd || be <= shiftStart)
            return new() { (shiftStart, shiftEnd) };

        // Clamp break vào shift range.
        bs = Math.Max(bs, shiftStart);
        be = Math.Min(be, shiftEnd);

        var segments = new List<(int Start, int End)>();
        if (bs > shiftStart) segments.Add((shiftStart, bs));
        if (be < shiftEnd) segments.Add((be, shiftEnd));
        return segments;
    }

    private static List<(int Start, int End)> BuildNightWindows(
        TimeOnly nightStart, TimeOnly nightEnd)
    {
        var ns = ToMinutes(nightStart);
        var ne = ToMinutes(nightEnd);
        var wraps = ne <= ns;

        if (!wraps)
            return new() { (ns, ne) };

        return new()
        {
            (ns - 1440, ne),       // hôm qua (vd [-120, 360])
            (ns, ne + 1440),       // hôm nay (vd [1320, 1800])
        };
    }

    private static int ToMinutes(TimeOnly t) => t.Hour * 60 + t.Minute;

    private static int ToMinutesFromBase(DateTime value, DateTime baseDate) =>
        (int)Math.Floor((value - baseDate).TotalMinutes);
}
