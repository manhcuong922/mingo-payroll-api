using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Common;

internal static class ShiftTemplateFutureUsageMessage
{
    private const int MaxScheduleNamesShown = 5;

    public static string Build(string shiftName, ShiftTemplateFutureUsage usage)
    {
        if (usage.ScheduleTemplateNames.Count > 0)
        {
            var shown = usage.ScheduleTemplateNames
                .Take(MaxScheduleNamesShown)
                .Select(n => $"'{n}'");
            var suffix = usage.ScheduleTemplateNames.Count > MaxScheduleNamesShown
                ? $" và {usage.ScheduleTemplateNames.Count - MaxScheduleNamesShown} bảng phân ca khác"
                : string.Empty;
            return $"Không thể ngừng ca '{shiftName}' vì đang được sử dụng bởi {usage.ScheduleTemplateNames.Count} bảng phân ca: "
                 + $"{string.Join(", ", shown)}{suffix}. "
                 + "Hãy gỡ ca khỏi bảng phân ca trước khi ngừng.";
        }

        // Chỉ còn phân ca ad-hoc (không thuộc bảng phân ca nào — do BatchUpdateShiftAssignments trực tiếp).
        return $"Không thể ngừng ca '{shiftName}' vì đang được phân trực tiếp cho nhân viên trong tương lai. "
             + "Hãy gỡ các phân ca này trước khi ngừng.";
    }
}
