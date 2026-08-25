using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Services;
using MPS.Infrastructure.Persistence;
using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Infrastructure.Services;

/// <summary>
/// BR-23 — Resolve DayType theo thứ tự ưu tiên:
/// (1) PublicHoliday → (2) PaidLeaveDay (LeaveRequest Approved + IsPaid) → (3) WeeklyRestDay → (4) Normal.
/// Ngày lễ áp dụng cho toàn bộ nhân viên (không còn scope).
///
/// <para>⭐ RESTDAY-2026-08 — ngày nghỉ hằng tuần đọc từ <b>cấu hình chấm công</b>
/// (<c>AttendanceSetting.WeeklyRestDayMask</c>), không còn suy từ bảng phân ca. Điều 111 BLLĐ để
/// doanh nghiệp ẤN ĐỊNH ngày nghỉ hằng tuần trong nội quy lao động — đó là một tuyên bố, không phải
/// thứ đoán được từ việc hôm đó có ai xếp ca hay chưa. Lý do đầy đủ (kèm hai chiều sai của bản A2
/// cũ) nằm ở <see cref="WeeklyRestDayPolicy"/>.</para>
/// </summary>
public class DayTypeResolver : IDayTypeResolver
{
    private readonly AppDbContext _context;

    // Cấu hình chấm công chỉ có MỘT dòng và không đổi trong vòng đời 1 request (service Scoped),
    // trong khi collector gọi resolver một lần cho MỖI nhân viên ⇒ cache lại để khỏi N+1.
    private int? _restDayMask;
    private bool _restDayMaskLoaded;

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

        return ResolveRestDay(workDate, await GetRestDayMaskAsync(cancellationToken));
    }

    public async Task<IReadOnlyDictionary<DateOnly, AttendanceEnums.DayType>> ResolveRangeAsync(
        int employeeInformationId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (toDate < fromDate)
            return new Dictionary<DateOnly, AttendanceEnums.DayType>();

        var holidaySet = await LoadHolidayDaysAsync(fromDate, toDate, cancellationToken);
        var paidLeaveDays = await LoadPaidLeaveDaysAsync(
            employeeInformationId, fromDate, toDate, cancellationToken);
        var mask = await GetRestDayMaskAsync(cancellationToken);

        var result = new Dictionary<DateOnly, AttendanceEnums.DayType>();
        for (var d = fromDate; d <= toDate; d = d.AddDays(1))
        {
            if (holidaySet.Contains(d))
                result[d] = AttendanceEnums.DayType.PublicHoliday;
            else if (paidLeaveDays.Contains(d))
                result[d] = AttendanceEnums.DayType.PaidLeaveDay;
            else
                result[d] = ResolveRestDay(d, mask);
        }
        return result;
    }

    /// <summary>
    /// ⭐ RESTDAY-2026-08 — nay ĐỒNG NHẤT với <see cref="ResolveRangeAsync"/>.
    ///
    /// <para>Bản cũ phải dò DOW-pattern của 28 ngày TRƯỚC khoảng đang add để thoát vòng luẩn quẩn
    /// "chưa có ca ⇒ mọi ngày là rest day ⇒ không tạo được ca". Vòng đó sinh ra từ chính việc suy
    /// ngày nghỉ tuần từ roster; đọc thẳng cấu hình thì không còn gì để luẩn quẩn. Giữ lại method
    /// để 4 call site (xếp ca, đơn nghỉ phép, đơn OT, đơn WFH) không phải đổi.</para>
    /// </summary>
    public Task<IReadOnlyDictionary<DateOnly, AttendanceEnums.DayType>> ResolveGuardRangeAsync(
        int employeeInformationId,
        DateOnly addFrom,
        DateOnly addTo,
        CancellationToken cancellationToken = default) =>
        ResolveRangeAsync(employeeInformationId, addFrom, addTo, cancellationToken);

    private async Task<HashSet<DateOnly>> LoadHolidayDaysAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var holidays = await _context.PublicHolidays.AsNoTracking()
            .Where(h => h.IsActive && h.HolidayDate <= toDate && h.HolidayEndDate >= fromDate)
            .Select(h => new { h.HolidayDate, h.HolidayEndDate })
            .ToListAsync(cancellationToken);

        var set = new HashSet<DateOnly>();
        foreach (var holiday in holidays)
        {
            var start = holiday.HolidayDate < fromDate ? fromDate : holiday.HolidayDate;
            var end = holiday.HolidayEndDate > toDate ? toDate : holiday.HolidayEndDate;
            for (var d = start; d <= end; d = d.AddDays(1))
                set.Add(d);
        }
        return set;
    }

    private async Task<HashSet<DateOnly>> LoadPaidLeaveDaysAsync(
        int employeeInformationId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var paidLeaves = await _context.LeaveRequests.AsNoTracking()
            .Where(l => l.EmployeeInformationId == employeeInformationId
                       && l.Status == AttendanceEnums.LeaveRequestStatus.Approved
                       && l.IsPaid
                       && l.StartDate <= toDate
                       && l.EndDate >= fromDate)
            .Select(l => new { l.StartDate, l.EndDate })
            .ToListAsync(cancellationToken);

        var set = new HashSet<DateOnly>();
        foreach (var leave in paidLeaves)
        {
            var start = leave.StartDate < fromDate ? fromDate : leave.StartDate;
            var end = leave.EndDate > toDate ? toDate : leave.EndDate;
            for (var d = start; d <= end; d = d.AddDays(1))
                set.Add(d);
        }
        return set;
    }

    /// <summary>
    /// Mask ngày nghỉ hằng tuần từ cấu hình chấm công. Chưa có dòng cấu hình, hoặc có mà để trống
    /// ⇒ trả null và <see cref="WeeklyRestDayPolicy"/> lùi về mặc định T7 + CN.
    /// </summary>
    private async Task<int?> GetRestDayMaskAsync(CancellationToken cancellationToken)
    {
        if (_restDayMaskLoaded) return _restDayMask;

        _restDayMask = await _context.AttendanceSettings.AsNoTracking()
            .OrderBy(s => s.Id)
            .Select(s => s.WeeklyRestDayMask)
            .FirstOrDefaultAsync(cancellationToken);
        _restDayMaskLoaded = true;
        return _restDayMask;
    }

    private static AttendanceEnums.DayType ResolveRestDay(DateOnly date, int? mask) =>
        WeeklyRestDayPolicy.IsRestDay(date, mask)
            ? AttendanceEnums.DayType.WeeklyRestDay
            : AttendanceEnums.DayType.Normal;
}
