using MPS.Application.Features.Attendance.EmployeeShift.Commands;
using MPS.Domain.Enums.Attendance;

namespace MPS.API.Requests.Attendance;

public sealed record CreateShiftTemplateRequest(
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeOnly? BreakStartTime,
    TimeOnly? BreakEndTime,
    decimal StandardHours,
    decimal StandardDays,
    decimal WorkRateNormal,
    decimal WorkRateWeeklyRest,
    decimal WorkRateHoliday,
    bool IsNightShift,
    int? AppliedAttendanceRuleId,
    string? Notes);

public sealed record UpdateShiftTemplateRequest(
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeOnly? BreakStartTime,
    TimeOnly? BreakEndTime,
    decimal StandardHours,
    decimal StandardDays,
    bool IsNightShift,
    int? AppliedAttendanceRuleId,
    bool IsActive,
    string? Notes);

public sealed record UpdateShiftPayRatesRequest(
    decimal WorkRateNormal,
    decimal WorkRateWeeklyRest,
    decimal WorkRateHoliday);

public sealed record ReapplyAttendanceRuleRequest(int AttendanceRuleId);

public sealed record CreateAttendanceRuleRequest(
    string Name,
    string? Description,
    decimal WorkRateNormal,
    decimal WorkRateWeeklyRest,
    decimal WorkRateHoliday);

public sealed record UpdateAttendanceRuleRequest(
    string Name,
    string? Description,
    decimal WorkRateNormal,
    decimal WorkRateWeeklyRest,
    decimal WorkRateHoliday,
    bool IsActive);

public sealed record BatchShiftOperationRequest(
    BatchShiftOperationKind Kind,
    int EmployeeInformationId,
    DateOnly WorkDate,
    int? ShiftTemplateId);

public sealed record BatchUpdateShiftAssignmentsRequest(
    IReadOnlyList<BatchShiftOperationRequest> Operations,
    BatchShiftConflictPolicy ConflictPolicy);
