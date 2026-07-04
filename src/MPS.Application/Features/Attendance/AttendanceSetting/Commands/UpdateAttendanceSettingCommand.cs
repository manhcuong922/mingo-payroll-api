using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceSetting.Commands;

public sealed record UpdateAttendanceSettingCommand(
    bool IsEditLockEnabled,
    TimeOnly DailyEditCutoffTime,
    int LockAfterDays,
    bool AllowAdminOverride,
    bool RequireOverrideReason,
    string? TimezoneId,
    string? Notes) : IRequest<AttendanceSettingDto>;
