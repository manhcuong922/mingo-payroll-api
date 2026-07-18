using MPS.Application.DTOs.Attendance;
using AttendanceRuleVersionEntity = MPS.Domain.Entities.Attendance.AttendanceRuleVersion;
using ShiftTemplateEntity = MPS.Domain.Entities.Attendance.ShiftTemplate;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Queries;

/// <summary>
/// Map ShiftTemplate entity → DTO với nested AttendanceRuleSummary.
/// Repository luôn Include(t => t.AppliedAttendanceRule); rate snapshot lấy từ open version
/// (bắn 0 nếu rule chưa có open version — edge case, không xảy ra ở luồng bình thường).
/// </summary>
internal static class ShiftTemplateMapper
{
    public static ShiftTemplateDto ToDto(ShiftTemplateEntity t, AttendanceRuleVersionEntity? openVersion)
    {
        var rule = t.AppliedAttendanceRule;
        var summary = new AttendanceRuleSummaryDto(
            rule.Id,
            rule.Name,
            openVersion?.WorkRateNormal ?? 0m,
            openVersion?.WorkRateWeeklyRest ?? 0m,
            openVersion?.WorkRateHoliday ?? 0m,
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
