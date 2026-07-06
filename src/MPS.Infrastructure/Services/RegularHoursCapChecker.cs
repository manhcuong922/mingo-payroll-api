using System.Globalization;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Infrastructure.Services;

/// <summary>
/// Checks regular working-hour caps from Điều 105 BLLĐ.
/// Caps (8h/day, 48h/week) are read from <c>LaborLawConstantsVersion</c> via
/// <see cref="IComplianceLookupService"/> — not hardcoded.
/// Caller is responsible for computing each delta correctly for add/remove/update.
/// </summary>
public sealed class RegularHoursCapChecker : IRegularHoursCapChecker
{
    private readonly IEmployeeShiftAssignmentRepository _assignmentRepository;
    private readonly IShiftTemplateRepository _shiftRepository;
    private readonly IAttendanceRecordRepository _attendanceRepository;
    private readonly IComplianceLookupService _complianceLookup;

    public RegularHoursCapChecker(
        IEmployeeShiftAssignmentRepository assignmentRepository,
        IShiftTemplateRepository shiftRepository,
        IAttendanceRecordRepository attendanceRepository,
        IComplianceLookupService complianceLookup)
    {
        _assignmentRepository = assignmentRepository;
        _shiftRepository = shiftRepository;
        _attendanceRepository = attendanceRepository;
        _complianceLookup = complianceLookup;
    }

    public async Task<IReadOnlyList<DailyCapWarning>> CheckDailyAssignmentsAsync(
        IEnumerable<WeeklyCapDelta> deltas,
        CancellationToken cancellationToken = default)
    {
        var grouped = GroupByEmployeeDate(deltas);
        if (grouped.Count == 0) return Array.Empty<DailyCapWarning>();

        var laborLaw = await _complianceLookup.GetActiveLaborLawConstantsAsync(cancellationToken);
        var maxPerDay = laborLaw.StandardWorkingHoursPerDay;

        var employeeIds = grouped.Select(g => g.Key.EmployeeId).Distinct().ToList();
        var fromDate = grouped.Min(g => g.Key.WorkDate);
        var toDate = grouped.Max(g => g.Key.WorkDate);

        var existing = await _assignmentRepository.GetByEmployeesAndDateRangeAsync(
            employeeIds, fromDate, toDate, cancellationToken);

        var shiftHours = await LoadShiftHoursAsync(
            existing.Select(e => e.ShiftTemplateId).Distinct(),
            cancellationToken);

        var warnings = new List<DailyCapWarning>();
        foreach (var group in grouped)
        {
            var (empId, workDate) = group.Key;
            var existingHours = existing
                .Where(a => a.EmployeeInformationId == empId && a.WorkDate == workDate)
                .Sum(a => shiftHours.TryGetValue(a.ShiftTemplateId, out var h) ? h : 0m);

            var totalAfter = existingHours + group.Value;
            if (totalAfter > maxPerDay)
            {
                warnings.Add(new DailyCapWarning(
                    empId, workDate, totalAfter, maxPerDay));
            }
        }

        return warnings;
    }

    public async Task<IReadOnlyList<WeeklyCapWarning>> CheckAssignmentsAsync(
        IEnumerable<WeeklyCapDelta> deltas,
        CancellationToken cancellationToken = default)
    {
        var grouped = GroupByEmployeeWeek(deltas);
        if (grouped.Count == 0) return Array.Empty<WeeklyCapWarning>();

        var laborLaw = await _complianceLookup.GetActiveLaborLawConstantsAsync(cancellationToken);
        var maxPerWeek = laborLaw.MaxRegularHoursPerWeek;

        var employeeIds = grouped.Select(g => g.Key.EmployeeId).Distinct().ToList();
        var fromDate = grouped.Min(g => g.Key.WeekStart);
        var toDate = grouped.Max(g => g.Key.WeekEnd);

        var existing = await _assignmentRepository.GetByEmployeesAndDateRangeAsync(
            employeeIds, fromDate, toDate, cancellationToken);

        var shiftHours = await LoadShiftHoursAsync(
            existing.Select(e => e.ShiftTemplateId).Distinct(),
            cancellationToken);

        var warnings = new List<WeeklyCapWarning>();
        foreach (var group in grouped)
        {
            var (empId, isoYear, isoWeek, weekStart, weekEnd) = group.Key;
            var existingHours = existing
                .Where(a => a.EmployeeInformationId == empId
                            && a.WorkDate >= weekStart
                            && a.WorkDate <= weekEnd)
                .Sum(a => shiftHours.TryGetValue(a.ShiftTemplateId, out var h) ? h : 0m);

            var totalAfter = existingHours + group.Value;
            if (totalAfter > maxPerWeek)
            {
                warnings.Add(new WeeklyCapWarning(
                    empId, isoYear, isoWeek, weekStart, weekEnd, totalAfter, maxPerWeek));
            }
        }

        return warnings;
    }

    public async Task<IReadOnlyList<DailyCapWarning>> CheckDailyAttendanceAsync(
        IEnumerable<WeeklyCapDelta> deltas,
        CancellationToken cancellationToken = default)
    {
        var grouped = GroupByEmployeeDate(deltas);
        if (grouped.Count == 0) return Array.Empty<DailyCapWarning>();

        var laborLaw = await _complianceLookup.GetActiveLaborLawConstantsAsync(cancellationToken);
        var maxPerDay = laborLaw.StandardWorkingHoursPerDay;

        var warnings = new List<DailyCapWarning>();
        foreach (var group in grouped)
        {
            var (empId, workDate) = group.Key;
            var records = await _attendanceRepository.GetByEmployeeRangeAsync(
                empId, workDate, workDate, cancellationToken);
            var existingHours = records.Sum(r => r.DayHours + r.NightHours);

            var totalAfter = existingHours + group.Value;
            if (totalAfter > maxPerDay)
            {
                warnings.Add(new DailyCapWarning(
                    empId, workDate, totalAfter, maxPerDay));
            }
        }

        return warnings;
    }

    public async Task<IReadOnlyList<WeeklyCapWarning>> CheckAttendanceAsync(
        IEnumerable<WeeklyCapDelta> deltas,
        CancellationToken cancellationToken = default)
    {
        var grouped = GroupByEmployeeWeek(deltas);
        if (grouped.Count == 0) return Array.Empty<WeeklyCapWarning>();

        var laborLaw = await _complianceLookup.GetActiveLaborLawConstantsAsync(cancellationToken);
        var maxPerWeek = laborLaw.MaxRegularHoursPerWeek;

        var warnings = new List<WeeklyCapWarning>();
        foreach (var group in grouped)
        {
            var (empId, isoYear, isoWeek, weekStart, weekEnd) = group.Key;
            var records = await _attendanceRepository.GetByEmployeeRangeAsync(
                empId, weekStart, weekEnd, cancellationToken);
            var existingHours = records.Sum(r => r.DayHours + r.NightHours);

            var totalAfter = existingHours + group.Value;
            if (totalAfter > maxPerWeek)
            {
                warnings.Add(new WeeklyCapWarning(
                    empId, isoYear, isoWeek, weekStart, weekEnd, totalAfter, maxPerWeek));
            }
        }

        return warnings;
    }

    private async Task<Dictionary<int, decimal>> LoadShiftHoursAsync(
        IEnumerable<int> shiftTemplateIds,
        CancellationToken cancellationToken)
    {
        var shiftHours = new Dictionary<int, decimal>();
        foreach (var shiftId in shiftTemplateIds)
        {
            var shift = await _shiftRepository.GetByIdAsync(shiftId, cancellationToken);
            if (shift is not null)
                shiftHours[shiftId] = shift.StandardHours;
        }
        return shiftHours;
    }

    private static Dictionary<DayKey, decimal> GroupByEmployeeDate(IEnumerable<WeeklyCapDelta> deltas)
    {
        var result = new Dictionary<DayKey, decimal>();
        foreach (var delta in deltas)
        {
            var key = new DayKey(delta.EmployeeInformationId, delta.WorkDate);
            result[key] = (result.TryGetValue(key, out var existing) ? existing : 0m) + delta.AdditionalHours;
        }
        return result;
    }

    private static Dictionary<WeekKey, decimal> GroupByEmployeeWeek(IEnumerable<WeeklyCapDelta> deltas)
    {
        var result = new Dictionary<WeekKey, decimal>();
        foreach (var delta in deltas)
        {
            var key = MakeWeekKey(delta.EmployeeInformationId, delta.WorkDate);
            result[key] = (result.TryGetValue(key, out var existing) ? existing : 0m) + delta.AdditionalHours;
        }
        return result;
    }

    private static WeekKey MakeWeekKey(int employeeId, DateOnly workDate)
    {
        var date = workDate.ToDateTime(TimeOnly.MinValue);
        var isoWeek = ISOWeek.GetWeekOfYear(date);
        var isoYear = ISOWeek.GetYear(date);
        var dayOfWeek = (int)date.DayOfWeek;
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        var weekStart = workDate.AddDays(-daysFromMonday);
        var weekEnd = weekStart.AddDays(6);
        return new WeekKey(employeeId, isoYear, isoWeek, weekStart, weekEnd);
    }

    private readonly record struct DayKey(
        int EmployeeId,
        DateOnly WorkDate);

    private readonly record struct WeekKey(
        int EmployeeId,
        int IsoYear,
        int IsoWeek,
        DateOnly WeekStart,
        DateOnly WeekEnd);
}
