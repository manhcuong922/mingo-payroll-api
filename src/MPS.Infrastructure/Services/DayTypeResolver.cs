using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces;
using MPS.Infrastructure.Persistence;
using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Infrastructure.Services;

/// <summary>
/// BR-23 — Resolve DayType theo thứ tự ưu tiên:
/// (1) PublicHoliday → (2) PaidLeaveDay (LeaveRequest Approved + IsPaid) → (3) WeeklyRestDay (Thứ 7 + Chủ nhật cố định) → (4) Normal.
/// Ngày lễ áp dụng cho toàn bộ nhân viên (không còn scope).
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
        var isHoliday = await _context.PublicHolidays.AsNoTracking()
            .AnyAsync(h => h.HolidayDate <= workDate && h.HolidayEndDate >= workDate && h.IsActive,
                cancellationToken);
        if (isHoliday)
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

        var holidays = await _context.PublicHolidays.AsNoTracking()
            .Where(h => h.IsActive && h.HolidayDate <= toDate && h.HolidayEndDate >= fromDate)
            .Select(h => new { h.HolidayDate, h.HolidayEndDate })
            .ToListAsync(cancellationToken);

        var holidaySet = new HashSet<DateOnly>();
        foreach (var holiday in holidays)
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
}
