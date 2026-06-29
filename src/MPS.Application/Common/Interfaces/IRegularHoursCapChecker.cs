namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Check caps for regular working hours (Điều 105 BLLĐ) using warn-then-confirm.
/// </summary>
public interface IRegularHoursCapChecker
{
    /// <summary>
    /// Daily cap for EmployeeShiftAssignment changes.
    /// Existing hours come from ShiftTemplate.StandardHours for assignments on the same day.
    /// </summary>
    Task<IReadOnlyList<DailyCapWarning>> CheckDailyAssignmentsAsync(
        IEnumerable<WeeklyCapDelta> deltas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Weekly cap for EmployeeShiftAssignment changes.
    /// Existing hours come from ShiftTemplate.StandardHours for assignments in the ISO week.
    /// </summary>
    Task<IReadOnlyList<WeeklyCapWarning>> CheckAssignmentsAsync(
        IEnumerable<WeeklyCapDelta> deltas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Daily cap for AttendanceRecord changes.
    /// Existing hours come from DayHours + NightHours for records on the same day.
    /// </summary>
    Task<IReadOnlyList<DailyCapWarning>> CheckDailyAttendanceAsync(
        IEnumerable<WeeklyCapDelta> deltas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Weekly cap for AttendanceRecord changes.
    /// Existing hours come from DayHours + NightHours for records in the ISO week.
    /// </summary>
    Task<IReadOnlyList<WeeklyCapWarning>> CheckAttendanceAsync(
        IEnumerable<WeeklyCapDelta> deltas,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Hour delta for one (employee, workDate). Caller computes delta by operation:
/// Add = +hours, Remove = -hours, Change/Update = newHours - oldHours.
/// </summary>
public sealed record WeeklyCapDelta(
    int EmployeeInformationId,
    DateOnly WorkDate,
    decimal AdditionalHours);

public sealed record DailyCapWarning(
    int EmployeeInformationId,
    DateOnly WorkDate,
    decimal TotalHoursAfter,
    decimal CapHours);

public sealed record WeeklyCapWarning(
    int EmployeeInformationId,
    int IsoYear,
    int IsoWeek,
    DateOnly WeekStart,
    DateOnly WeekEnd,
    decimal TotalHoursAfter,
    decimal CapHours);
