using MPS.Application.DTOs.Attendance;
using ShiftTemplateEntity = MPS.Domain.Entities.Attendance.ShiftTemplate;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Queries;

/// <summary>
/// Map ShiftTemplate entity → DTO với nested AttendanceRuleSummary. Repository
/// luôn Include(t => t.AppliedAttendanceRule) nên rule không null sau §11.
/// </summary>
internal static class ShiftTemplateMapper
{
    public static ShiftTemplateDto ToDto(ShiftTemplateEntity t)
    {
        var rule = t.AppliedAttendanceRule;
        var summary = new AttendanceRuleSummaryDto(
            rule.Id,
            rule.Name,
            rule.WorkRateNormal,
            rule.WorkRateWeeklyRest,
            rule.WorkRateHoliday,
            rule.WorkRateNight,
            rule.IsActive);

        return new ShiftTemplateDto(
            t.Id,
            t.Code,
            t.Name,
            t.StartTime,
            t.EndTime,
            t.BreakStartTime,
            t.BreakEndTime,
            t.StandardHours,
            t.StandardDays,
            t.AppliedAttendanceRuleId,
            summary,
            t.IsActive,
            t.Notes,
            t.CreatedAt,
            t.UpdatedAt);
    }
}
