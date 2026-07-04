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
        setting.TimezoneId,
        setting.Notes,
        setting.CreatedAt,
        setting.UpdatedAt);
}
