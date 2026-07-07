namespace MPS.Application.Features.Attendance.WeeklyDayOffRule.Common;

internal static class DaysOfWeekMask
{
    public static string Encode(IEnumerable<int> daysOfWeek) =>
        string.Join(',', daysOfWeek
            .Where(d => d is >= 0 and <= 6)
            .Distinct()
            .OrderBy(d => d));

    public static IReadOnlyList<int> Decode(string? mask)
    {
        if (string.IsNullOrWhiteSpace(mask)) return Array.Empty<int>();

        return mask
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var v) ? v : -1)
            .Where(v => v is >= 0 and <= 6)
            .Distinct()
            .OrderBy(v => v)
            .ToArray();
    }
}
