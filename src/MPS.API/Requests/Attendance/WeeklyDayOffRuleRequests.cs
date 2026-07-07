namespace MPS.API.Requests.Attendance;

public sealed record CreateWeeklyDayOffRuleRequest(
    string Name,
    string? Description,
    IReadOnlyList<int> DaysOfWeek,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo = null,
    string ApplyScope = "AllEmployees",
    string? TargetDepartmentIds = null,
    string? TargetEmployeeIds = null);

public sealed record UpdateWeeklyDayOffRuleRequest(
    string Name,
    string? Description,
    IReadOnlyList<int> DaysOfWeek,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo = null,
    string ApplyScope = "AllEmployees",
    string? TargetDepartmentIds = null,
    string? TargetEmployeeIds = null);
