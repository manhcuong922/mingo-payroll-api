using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Infrastructure.Persistence;
using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Infrastructure.Services;

/// <inheritdoc cref="IEmployeeDayOffChecker"/>
public class EmployeeDayOffChecker : IEmployeeDayOffChecker
{
    private readonly AppDbContext _context;

    public EmployeeDayOffChecker(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DayOffStatusDto> CheckAsync(
        int employeeInformationId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        // (1) Đơn nghỉ phép đã duyệt — chặn bất kể có được phân ca hay không.
        // Không lọc IsPaid: nghỉ không lương cũng là nghỉ.
        var leaveTypeName = await _context.LeaveRequests.AsNoTracking()
            .Where(l => l.EmployeeInformationId == employeeInformationId
                        && l.Status == AttendanceEnums.LeaveRequestStatus.Approved
                        && l.StartDate <= date
                        && l.EndDate >= date)
            .Select(l => l.LeaveType.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (leaveTypeName is not null)
        {
            return new DayOffStatusDto(
                date,
                IsDayOff: true,
                Reason: DayOffReasons.Leave,
                Message: $"Hôm nay bạn nghỉ theo đơn đã duyệt ({leaveTypeName}).",
                DetailName: leaveTypeName);
        }

        var isHoliday = await _context.PublicHolidays.AsNoTracking()
            .AnyAsync(h => h.IsActive && h.HolidayDate <= date && h.HolidayEndDate >= date,
                cancellationToken);

        var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        // Ngày lễ / cuối tuần mà vẫn được phân ca nghĩa là chủ ý cho đi làm → không phải ngày nghỉ.
        if (!isHoliday && !isWeekend)
            return NotDayOff(date);

        var hasShift = await _context.EmployeeShiftAssignments.AsNoTracking()
            .AnyAsync(a => a.EmployeeInformationId == employeeInformationId && a.WorkDate == date,
                cancellationToken);

        if (hasShift)
            return NotDayOff(date);

        // (2) Ngày lễ.
        if (isHoliday)
        {
            var holidayName = await _context.PublicHolidays.AsNoTracking()
                .Where(h => h.IsActive && h.HolidayDate <= date && h.HolidayEndDate >= date)
                .Select(h => h.Name)
                .FirstOrDefaultAsync(cancellationToken);

            return new DayOffStatusDto(
                date,
                IsDayOff: true,
                Reason: DayOffReasons.PublicHoliday,
                Message: holidayName is null
                    ? "Hôm nay là ngày lễ, bạn được nghỉ."
                    : $"Hôm nay là ngày lễ ({holidayName}), bạn được nghỉ.",
                DetailName: holidayName);
        }

        // (3) Cuối tuần.
        return new DayOffStatusDto(
            date,
            IsDayOff: true,
            Reason: DayOffReasons.WeeklyRest,
            Message: "Hôm nay là ngày nghỉ cuối tuần, bạn không có ca làm.",
            DetailName: null);
    }

    private static DayOffStatusDto NotDayOff(DateOnly date) =>
        new(date, IsDayOff: false, Reason: null, Message: null, DetailName: null);
}
