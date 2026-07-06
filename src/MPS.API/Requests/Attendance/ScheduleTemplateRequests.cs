using MPS.Domain.Enums.Attendance;

namespace MPS.API.Requests.Attendance;

public sealed record CreateScheduleTemplateRequest(
    string Name,
    int ShiftTemplateId,
    IReadOnlyList<int>? ShiftTemplateIds,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo,
    ScheduleRepeatUnit RepeatUnit,
    int RepeatEvery,
    IReadOnlyList<int>? DaysOfWeek,
    IReadOnlyList<int>? DaysOfMonth,
    IReadOnlyList<int> EmployeeInformationIds,
    string? Notes,
    bool AcknowledgeWeeklyOvertimeWarnings = false,
    ScheduleApplyTarget? ApplyTarget = null,
    IReadOnlyList<int>? DepartmentIds = null);

public sealed record UpdateScheduleTemplateRequest(
    string Name,
    int ShiftTemplateId,
    IReadOnlyList<int>? ShiftTemplateIds,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo,
    ScheduleRepeatUnit RepeatUnit,
    int RepeatEvery,
    IReadOnlyList<int>? DaysOfWeek,
    IReadOnlyList<int>? DaysOfMonth,
    IReadOnlyList<int> EmployeeInformationIds,
    bool IsActive,
    string? Notes,
    ScheduleApplyTarget? ApplyTarget = null,
    IReadOnlyList<int>? DepartmentIds = null);

public sealed record ConfirmMonthlyScheduleImportRequest(
    string PreviewToken,
    bool AcknowledgeWeeklyOvertimeWarnings = false);
