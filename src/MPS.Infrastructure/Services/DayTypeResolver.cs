using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces;
using MPS.Infrastructure.Persistence;
using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Infrastructure.Services;

/// <summary>
/// BR-23 — Resolve DayType theo thứ tự ưu tiên:
/// (1) PublicHoliday → (2) PaidLeaveDay (LeaveRequest Approved + IsPaid) → (3) WeeklyRestDay (Thứ 7 + Chủ nhật cố định) → (4) Normal.
/// </summary>
public class DayTypeResolver : IDayTypeResolver
{
    private readonly AppDbContext _context;

    public DayTypeResolver(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AttendanceEnums.DayType> ResolveAsync(
        int employeeInformationId,
        DateOnly workDate,
        CancellationToken cancellationToken = default)
    {
        var departmentId = await GetEmployeeDepartmentIdAsync(employeeInformationId, cancellationToken);
        var holidays = await _context.PublicHolidays.AsNoTracking()
            .Where(h => h.HolidayDate <= workDate && h.HolidayEndDate >= workDate && h.IsActive)
            .Select(h => new ScopeTarget(h.ApplyScope, h.TargetDepartmentIds, h.TargetEmployeeIds))
            .ToListAsync(cancellationToken);

        if (holidays.Any(h => AppliesToEmployee(h, employeeInformationId, departmentId)))
            return AttendanceEnums.DayType.PublicHoliday;

        var hasPaidLeave = await _context.LeaveRequests.AsNoTracking()
            .AnyAsync(l => l.EmployeeInformationId == employeeInformationId
                          && l.Status == AttendanceEnums.LeaveRequestStatus.Approved
                          && l.IsPaid
                          && l.StartDate <= workDate
                          && l.EndDate >= workDate,
                cancellationToken);
        if (hasPaidLeave)
            return AttendanceEnums.DayType.PaidLeaveDay;

        if (IsWeekend(workDate))
            return AttendanceEnums.DayType.WeeklyRestDay;

        return AttendanceEnums.DayType.Normal;
    }

    public async Task<IReadOnlyDictionary<DateOnly, AttendanceEnums.DayType>> ResolveRangeAsync(
        int employeeInformationId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (toDate < fromDate)
            return new Dictionary<DateOnly, AttendanceEnums.DayType>();

        var departmentId = await GetEmployeeDepartmentIdAsync(employeeInformationId, cancellationToken);
        var holidays = await _context.PublicHolidays.AsNoTracking()
            .Where(h => h.IsActive && h.HolidayDate <= toDate && h.HolidayEndDate >= fromDate)
            .Select(h => new HolidayTarget(
                h.HolidayDate,
                h.HolidayEndDate,
                h.ApplyScope,
                h.TargetDepartmentIds,
                h.TargetEmployeeIds))
            .ToListAsync(cancellationToken);
        var holidaySet = new HashSet<DateOnly>();
        foreach (var holiday in holidays.Where(h => AppliesToEmployee(
            new ScopeTarget(h.ApplyScope, h.TargetDepartmentIds, h.TargetEmployeeIds),
            employeeInformationId,
            departmentId)))
        {
            var start = holiday.HolidayDate < fromDate ? fromDate : holiday.HolidayDate;
            var end = holiday.HolidayEndDate > toDate ? toDate : holiday.HolidayEndDate;
            for (var d = start; d <= end; d = d.AddDays(1))
                holidaySet.Add(d);
        }

        var paidLeaves = await _context.LeaveRequests.AsNoTracking()
            .Where(l => l.EmployeeInformationId == employeeInformationId
                       && l.Status == AttendanceEnums.LeaveRequestStatus.Approved
                       && l.IsPaid
                       && l.StartDate <= toDate
                       && l.EndDate >= fromDate)
            .Select(l => new { l.StartDate, l.EndDate })
            .ToListAsync(cancellationToken);

        var paidLeaveDays = new HashSet<DateOnly>();
        foreach (var leave in paidLeaves)
        {
            var start = leave.StartDate < fromDate ? fromDate : leave.StartDate;
            var end = leave.EndDate > toDate ? toDate : leave.EndDate;
            for (var d = start; d <= end; d = d.AddDays(1))
                paidLeaveDays.Add(d);
        }

        var result = new Dictionary<DateOnly, AttendanceEnums.DayType>();
        for (var d = fromDate; d <= toDate; d = d.AddDays(1))
        {
            if (holidaySet.Contains(d))
                result[d] = AttendanceEnums.DayType.PublicHoliday;
            else if (paidLeaveDays.Contains(d))
                result[d] = AttendanceEnums.DayType.PaidLeaveDay;
            else if (IsWeekend(d))
                result[d] = AttendanceEnums.DayType.WeeklyRestDay;
            else
                result[d] = AttendanceEnums.DayType.Normal;
        }
        return result;
    }

    private static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private Task<int?> GetEmployeeDepartmentIdAsync(
        int employeeInformationId,
        CancellationToken cancellationToken) =>
        _context.EmployeeInformations.AsNoTracking()
            .Where(e => e.Id == employeeInformationId)
            .Select(e => e.DepartmentId)
            .FirstOrDefaultAsync(cancellationToken);

    private static bool AppliesToEmployee(
        ScopeTarget target,
        int employeeInformationId,
        int? departmentId) =>
        NormalizeScope(target.ApplyScope) switch
        {
            "AllEmployees" => true,
            "AllDepartments" => departmentId.HasValue,
            "SpecificEmployees" => ParseIds(target.TargetEmployeeIds).Contains(employeeInformationId),
            "SpecificDepartments" => departmentId.HasValue
                && ParseIds(target.TargetDepartmentIds).Contains(departmentId.Value),
            _ => false
        };

    private static string NormalizeScope(string? scope) =>
        string.IsNullOrWhiteSpace(scope) ? "AllEmployees" : scope.Trim();

    private static IReadOnlySet<int> ParseIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new HashSet<int>();

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
    }

    private sealed record ScopeTarget(
        string? ApplyScope,
        string? TargetDepartmentIds,
        string? TargetEmployeeIds);

    private sealed record HolidayTarget(
        DateOnly HolidayDate,
        DateOnly HolidayEndDate,
        string? ApplyScope,
        string? TargetDepartmentIds,
        string? TargetEmployeeIds);
}
