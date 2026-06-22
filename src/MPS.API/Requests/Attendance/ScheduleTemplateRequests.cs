using MPS.Domain.Enums.Attendance;

namespace MPS.API.Requests.Attendance;

public sealed record CreateScheduleTemplateRequest(
    string Name,
    int ShiftTemplateId,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo,
    ScheduleRepeatUnit RepeatUnit,
    int RepeatEvery,
    IReadOnlyList<int>? DaysOfWeek,
    IReadOnlyList<int>? DaysOfMonth,
    IReadOnlyList<int> EmployeeInformationIds,
    string? Notes);

public sealed record UpdateScheduleTemplateRequest(
    string Name,
    int ShiftTemplateId,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo,
    ScheduleRepeatUnit RepeatUnit,
    int RepeatEvery,
    IReadOnlyList<int>? DaysOfWeek,
    IReadOnlyList<int>? DaysOfMonth,
    IReadOnlyList<int> EmployeeInformationIds,
    bool IsActive,
    string? Notes);
