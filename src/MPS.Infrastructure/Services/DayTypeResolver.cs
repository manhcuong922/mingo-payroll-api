using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces;
using MPS.Infrastructure.Persistence;
using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Infrastructure.Services;

/// <summary>
/// BR-23 — Resolve DayType theo thứ tự ưu tiên:
/// (1) PublicHoliday → (2) PaidLeaveDay (LeaveRequest Approved + IsPaid) → (3) WeeklyRestDay → (4) Normal.
/// Ngày lễ áp dụng cho toàn bộ nhân viên (không còn scope).
///
/// <para>⭐ A2 — ngày nghỉ tuần suy từ <b>bảng phân ca</b> (<c>EmployeeShiftAssignment</c>), không
/// hardcode T7+CN. Điều 111 BLLĐ chỉ yêu cầu tối thiểu 1 ngày nghỉ/tuần và để doanh nghiệp ấn định
/// theo lịch phân ca của từng người; công ty làm T2–T7 (rất phổ biến ở xưởng sản xuất) thì Thứ Bảy
/// là ngày làm việc bình thường. Hardcode làm mọi Thứ Bảy được trả phụ trội "làm ngày nghỉ tuần"
/// 100% và OT Thứ Bảy ăn 200% thay vì 150%.</para>
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

        // Cửa sổ dò lịch phân ca = TUẦN chứa ngày cần tra (T2→CN) — đủ để biết tuần đó NV có được
        // xếp lịch hay không mà không phải quét cả tháng.
        var (weekStart, weekEnd) = WeekOf(workDate);
        var assigned = await LoadAssignedDaysAsync(employeeInformationId, weekStart, weekEnd, cancellationToken);

        return ResolveRestDay(workDate, assigned);
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

        // A2 — dò lịch phân ca trên TOÀN khoảng hỏi (kỳ lương = cả tháng ⇒ thừa dữ liệu để kết luận).
        var assigned = await LoadAssignedDaysAsync(employeeInformationId, fromDate, toDate, cancellationToken);

        var result = new Dictionary<DateOnly, AttendanceEnums.DayType>();
        for (var d = fromDate; d <= toDate; d = d.AddDays(1))
        {
            if (holidaySet.Contains(d))
                result[d] = AttendanceEnums.DayType.PublicHoliday;
            else if (paidLeaveDays.Contains(d))
                result[d] = AttendanceEnums.DayType.PaidLeaveDay;
            else
                result[d] = ResolveRestDay(d, assigned);
        }
        return result;
    }

    /// <summary>
    /// Guard-before-add: suy WeeklyRestDay từ DOW-pattern của 28 ngày TRƯỚC khoảng
    /// đang add, không dùng chính khoảng add — nếu không assignments trong khoảng add
    /// (đúng ra là chưa tạo) sẽ tự biến mọi ngày thành rest day (vòng gà/trứng),
    /// đúng bug đã gặp khi edit template mở rộng sang tháng mới.
    ///
    /// <para>Lookback rỗng ⇒ fallback T7/CN (tôn trọng comment A2 cũ: NV mới chưa
    /// có lịch sử thì mặc định T7/CN, không thể suy pattern).</para>
    /// </summary>
    public async Task<IReadOnlyDictionary<DateOnly, AttendanceEnums.DayType>> ResolveGuardRangeAsync(
        int employeeInformationId,
        DateOnly addFrom,
        DateOnly addTo,
        CancellationToken cancellationToken = default)
    {
        if (addTo < addFrom)
            return new Dictionary<DateOnly, AttendanceEnums.DayType>();

        var holidays = await _context.PublicHolidays.AsNoTracking()
            .Where(h => h.IsActive && h.HolidayDate <= addTo && h.HolidayEndDate >= addFrom)
            .Select(h => new { h.HolidayDate, h.HolidayEndDate })
            .ToListAsync(cancellationToken);

        var holidaySet = new HashSet<DateOnly>();
        foreach (var holiday in holidays)
        {
            var start = holiday.HolidayDate < addFrom ? addFrom : holiday.HolidayDate;
            var end = holiday.HolidayEndDate > addTo ? addTo : holiday.HolidayEndDate;
            for (var d = start; d <= end; d = d.AddDays(1))
                holidaySet.Add(d);
        }

        var paidLeaves = await _context.LeaveRequests.AsNoTracking()
            .Where(l => l.EmployeeInformationId == employeeInformationId
                       && l.Status == AttendanceEnums.LeaveRequestStatus.Approved
                       && l.IsPaid
                       && l.StartDate <= addTo
                       && l.EndDate >= addFrom)
            .Select(l => new { l.StartDate, l.EndDate })
            .ToListAsync(cancellationToken);

        var paidLeaveDays = new HashSet<DateOnly>();
        foreach (var leave in paidLeaves)
        {
            var start = leave.StartDate < addFrom ? addFrom : leave.StartDate;
            var end = leave.EndDate > addTo ? addTo : leave.EndDate;
            for (var d = start; d <= end; d = d.AddDays(1))
                paidLeaveDays.Add(d);
        }

        // Cửa sổ dò lịch sử: 28 ngày trước addFrom (không bao gồm khoảng add).
        var lookbackFrom = addFrom.AddDays(-28);
        var lookbackTo = addFrom.AddDays(-1);
        var historicalAssigned = await LoadAssignedDaysAsync(
            employeeInformationId, lookbackFrom, lookbackTo, cancellationToken);
        HashSet<DayOfWeek>? workingDows = historicalAssigned.Count > 0
            ? historicalAssigned.Select(d => d.DayOfWeek).ToHashSet()
            : null;

        var result = new Dictionary<DateOnly, AttendanceEnums.DayType>();
        for (var d = addFrom; d <= addTo; d = d.AddDays(1))
        {
            if (holidaySet.Contains(d))
                result[d] = AttendanceEnums.DayType.PublicHoliday;
            else if (paidLeaveDays.Contains(d))
                result[d] = AttendanceEnums.DayType.PaidLeaveDay;
            else if (workingDows is null)
                result[d] = IsWeekend(d) ? AttendanceEnums.DayType.WeeklyRestDay : AttendanceEnums.DayType.Normal;
            else
                result[d] = workingDows.Contains(d.DayOfWeek)
                    ? AttendanceEnums.DayType.Normal
                    : AttendanceEnums.DayType.WeeklyRestDay;
        }
        return result;
    }

    private async Task<HashSet<DateOnly>> LoadAssignedDaysAsync(
        int employeeInformationId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken) =>
        (await _context.EmployeeShiftAssignments.AsNoTracking()
            .Where(a => a.EmployeeInformationId == employeeInformationId
                       && a.WorkDate >= fromDate && a.WorkDate <= toDate)
            .Select(a => a.WorkDate)
            .Distinct()
            .ToListAsync(cancellationToken))
        .ToHashSet();

    /// <summary>
    /// A2 — ngày nghỉ tuần là ngày NV <b>không được xếp ca</b>.
    ///
    /// <para>Khi khoảng dò KHÔNG có bản phân ca nào (<paramref name="assignedDays"/> rỗng) thì giữ
    /// quy ước cũ T7+CN. Nếu không, dữ liệu lịch sử chưa số hoá lịch phân ca sẽ biến MỌI ngày thành
    /// "ngày nghỉ tuần" và được trả phụ trội 100% — sai còn nặng hơn lỗi đang sửa.</para>
    /// </summary>
    private static AttendanceEnums.DayType ResolveRestDay(DateOnly date, IReadOnlySet<DateOnly> assignedDays)
    {
        if (assignedDays.Count == 0)
            return IsWeekend(date) ? AttendanceEnums.DayType.WeeklyRestDay : AttendanceEnums.DayType.Normal;

        return assignedDays.Contains(date)
            ? AttendanceEnums.DayType.Normal
            : AttendanceEnums.DayType.WeeklyRestDay;
    }

    /// <summary>Tuần chứa <paramref name="date"/>, quy ước Việt Nam: Thứ Hai → Chủ nhật.</summary>
    private static (DateOnly Start, DateOnly End) WeekOf(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7; // CN=0 → 6; T2=1 → 0; ...
        var start = date.AddDays(-offset);
        return (start, start.AddDays(6));
    }

    private static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
