using MPS.Application.DTOs.Attendance;
using AttendanceSettingEntity = MPS.Domain.Entities.Attendance.AttendanceSetting;

namespace MPS.Application.Features.Attendance.AttendanceSetting.Common;

internal static class AttendanceSettingMapper
{
    public static AttendanceSettingDto ToDto(AttendanceSettingEntity setting) => new(
        setting.Id,
        setting.IsEditLockEnabled,
        setting.DailyEditCutoffTime,
        setting.LockAfterDays,
        setting.AllowAdminOverride,
        setting.RequireOverrideReason,
        setting.LateEarlyMaxOccurrencesPerMonth,
        setting.LateEarlyMaxMinutesPerOccurrence,
        setting.LateEarlyBackdateDaysAllowed,
        setting.WfhMaxDaysPerMonth,
        setting.WfhMinNoticeDays,
        setting.WfhMaxDaysPerRequest,
        setting.WfhWorkPlanMinLength,
        setting.WeeklyRestDayMask,
        setting.LateGraceMinutes,
        setting.EarlyLeaveGraceMinutes,
        setting.HalfDayThresholdMinutes,
        setting.CreatedAt,
        setting.UpdatedAt);
}
