using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceSetting.Commands;

public sealed record UpdateAttendanceSettingCommand(
    bool IsEditLockEnabled,
    TimeOnly DailyEditCutoffTime,
    int LockAfterDays,
    bool AllowAdminOverride,
    bool RequireOverrideReason,
    int? LateEarlyMaxOccurrencesPerMonth,
    int? LateEarlyMaxMinutesPerOccurrence,
    int? LateEarlyBackdateDaysAllowed,
    decimal? WfhMaxDaysPerMonth,
    int? WfhMinNoticeDays,
    decimal? WfhMaxDaysPerRequest,
    int? WfhWorkPlanMinLength,
    int? LateGraceMinutes,
    int? EarlyLeaveGraceMinutes,
    int? HalfDayThresholdMinutes) : IRequest<AttendanceSettingDto>;
