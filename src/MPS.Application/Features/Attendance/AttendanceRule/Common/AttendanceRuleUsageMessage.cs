namespace MPS.Application.Features.Attendance.AttendanceRule.Common;

internal static class AttendanceRuleUsageMessage
{
    private const int MaxShiftNamesShown = 5;

    public static string Build(string ruleName, IReadOnlyList<string> activeShiftNames)
    {
        var shown = activeShiftNames
            .Take(MaxShiftNamesShown)
            .Select(n => $"'{n}'");
        var suffix = activeShiftNames.Count > MaxShiftNamesShown
            ? $" và {activeShiftNames.Count - MaxShiftNamesShown} ca khác"
            : string.Empty;

        return $"Không thể ngừng hệ số '{ruleName}' vì đang được sử dụng bởi {activeShiftNames.Count} ca làm việc: "
             + $"{string.Join(", ", shown)}{suffix}. "
             + "Hãy gán các ca này sang hệ số khác trước khi ngừng.";
    }
}
