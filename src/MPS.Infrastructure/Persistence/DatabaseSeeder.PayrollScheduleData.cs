using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Entities.Attendance;
using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// Dữ liệu liên kết giữa bảng phân ca, chấm công và đơn WFH của bộ demo lương.
/// Mọi bản ghi chấm công ca chính phải có một <see cref="EmployeeShiftAssignment"/> tương ứng.
/// </summary>
public sealed partial class DatabaseSeeder
{
    private const string PayrollSchedulePrefix = "[Demo lương] Phân ca";

    private async Task EnsurePayrollMonthScheduleAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        int defaultShiftTemplateId,
        int adminId,
        IReadOnlyCollection<int> employeeIds,
        IReadOnlyCollection<DateOnly> standardWorkdays,
        CancellationToken ct)
    {
        var name = $"{PayrollSchedulePrefix} {periodStart:MM/yyyy}";
        var schedule = await _context.ScheduleTemplates
            .Include(s => s.Employees)
            .Include(s => s.Shifts)
            .FirstOrDefaultAsync(s => s.Name == name, ct);

        if (schedule is null)
        {
            schedule = new ScheduleTemplate
            {
                Name = name,
                ShiftTemplateId = defaultShiftTemplateId,
                EffectiveFrom = periodStart,
                EffectiveTo = periodEnd,
                RepeatUnit = AttendanceEnums.ScheduleRepeatUnit.Week,
                RepeatEvery = 1,
                DaysOfWeekMask = "1,2,3,4,5",
                IsActive = true,
                IncludeRestDaysAndHolidays = false,
                ApplyTarget = AttendanceEnums.ScheduleApplyTarget.Employees,
                Notes = "Bảng phân ca nguồn cho bộ dữ liệu demo tính lương; sinh idempotent theo tháng.",
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = adminId,
            };
            await _context.ScheduleTemplates.AddAsync(schedule, ct);
            await _context.SaveChangesAsync(ct);
        }

        var existingEmployeeIds = schedule.Employees.Select(e => e.EmployeeInformationId).ToHashSet();
        foreach (var employeeId in employeeIds.Where(id => !existingEmployeeIds.Contains(id)))
            schedule.Employees.Add(new ScheduleTemplateEmployee { EmployeeInformationId = employeeId });

        // Những ngày làm thêm theo lịch đặc biệt (thứ Bảy/Chủ nhật) cũng phải xuất hiện trong
        // template và assignment; toàn bộ ca chính vẫn dùng đúng mẫu FULLTIME 08:00–17:00.
        var attendanceShifts = await _context.AttendanceRecords.AsNoTracking()
            .Where(a => employeeIds.Contains(a.EmployeeInformationId)
                && a.WorkDate >= periodStart && a.WorkDate <= periodEnd
                && a.ShiftTemplateId != null)
            .Select(a => new { a.EmployeeInformationId, a.WorkDate, ShiftTemplateId = a.ShiftTemplateId!.Value })
            .ToListAsync(ct);

        var requiredShiftIds = attendanceShifts.Select(a => a.ShiftTemplateId)
            .Append(defaultShiftTemplateId).Distinct().ToList();
        var existingShiftIds = schedule.Shifts.Select(s => s.ShiftTemplateId).ToHashSet();
        foreach (var shiftId in requiredShiftIds.Where(id => !existingShiftIds.Contains(id)))
            schedule.Shifts.Add(new ScheduleTemplateShift { ShiftTemplateId = shiftId });

        await _context.SaveChangesAsync(ct);

        var desired = new HashSet<(int EmployeeId, DateOnly WorkDate, int ShiftId)>();
        var datesWithSeededAttendance = attendanceShifts
            .Select(a => (a.EmployeeInformationId, a.WorkDate))
            .ToHashSet();
        foreach (var employeeId in employeeIds)
        {
            var (from, to) = await LoadContractWindowAsync(employeeId, periodStart, ct);
            if (from is null) continue;

            foreach (var day in standardWorkdays)
            {
                if (day < from || (to is DateOnly last && day > last)) continue;
                if (datesWithSeededAttendance.Contains((employeeId, day))) continue;
                desired.Add((employeeId, day, defaultShiftTemplateId));
            }
        }

        foreach (var a in attendanceShifts)
            desired.Add((a.EmployeeInformationId, a.WorkDate, a.ShiftTemplateId));

        var existing = (await _context.EmployeeShiftAssignments.AsNoTracking()
            .Where(a => employeeIds.Contains(a.EmployeeInformationId)
                && a.WorkDate >= periodStart && a.WorkDate <= periodEnd)
            .Select(a => new { a.EmployeeInformationId, a.WorkDate, a.ShiftTemplateId })
            .ToListAsync(ct))
            .Select(a => (a.EmployeeInformationId, a.WorkDate, a.ShiftTemplateId))
            .ToHashSet();

        var now = DateTime.UtcNow;
        var rows = desired.Where(d => !existing.Contains(d))
            .Select(d => new EmployeeShiftAssignment
            {
                EmployeeInformationId = d.EmployeeId,
                WorkDate = d.WorkDate,
                ShiftTemplateId = d.ShiftId,
                ScheduleTemplateId = schedule.Id,
                CreatedAt = now,
                CreatedByUserId = adminId,
            })
            .ToList();

        if (rows.Count > 0)
        {
            await _context.EmployeeShiftAssignments.AddRangeAsync(rows, ct);
            await _context.SaveChangesAsync(ct);
        }

        _logger.LogInformation(
            "Payroll schedule {Month:MM/yyyy}: {Employees} nhân viên, bổ sung {Assignments} lượt phân ca.",
            periodStart.ToDateTime(TimeOnly.MinValue), employeeIds.Count, rows.Count);
    }

    /// <summary>
    /// Bốn nhân viên cùng đăng ký WFH 23–25/08/2026. Ngày Chủ nhật không có phân ca nên đơn vẫn
    /// giữ khoảng 23–25 nhưng chỉ tự sinh công cho các ngày làm việc đã được phân ca (24 và 25).
    /// Đây chính là hành vi của <c>WfhRequestRepository.ApproveAsync</c>, không ghi cứng kết quả lương.
    /// </summary>
    private Task SeedWfhDemoCasesAsync(int adminId, CancellationToken ct)
    {
        var start = DemoCurrentMonth.AddDays(22);
        var end = DemoCurrentMonth.AddDays(24);
        var usernames = new[] { "hr", "accountant", "cn_x1_2", "cn_x3_3" };

        return SeedWfhCasesAsync(
            start, end, usernames,
            "Làm việc từ xa trong thời gian xử lý công việc cá nhân tại nhà.", adminId, ct);
    }

    /// <summary>
    /// WFH07 — bốn nhân viên thuộc nhiều bộ phận làm việc từ xa 13–15/07. Case nằm ngay trong kỳ
    /// demo chính nên đi xuyên qua phân ca, chấm công, collector tới số công và thực nhận.
    /// </summary>
    private Task SeedJulyWfhPayrollCaseAsync(int adminId, CancellationToken ct) =>
        SeedWfhCasesAsync(
            DemoDay(2, 13), DemoDay(2, 15), new[] { "quandoc_x3", "hr", "accountant", "cn_x1_2" },
            "Làm việc từ xa ba ngày theo kế hoạch đã đăng ký; cập nhật tiến độ và họp trực tuyến hằng ngày.", adminId, ct);

    private async Task SeedWfhCasesAsync(
        DateOnly start,
        DateOnly end,
        IReadOnlyCollection<string> usernames,
        string reason,
        int adminId,
        CancellationToken ct)
    {

        var employees = await _context.EmployeeInformations.AsNoTracking()
            .Where(e => e.UserId != null && e.User != null && usernames.Contains(e.User.Username))
            .Select(e => new { e.Id, UserId = e.UserId!.Value, e.User!.Username })
            .ToListAsync(ct);

        foreach (var employee in employees)
        {
            var exists = await _context.WfhRequests.AsNoTracking()
                .AnyAsync(r => r.EmployeeInformationId == employee.Id
                    && r.StartDate == start && r.EndDate == end, ct);
            if (exists) continue;

            var assignments = await _context.EmployeeShiftAssignments.AsNoTracking()
                .Where(a => a.EmployeeInformationId == employee.Id
                    && a.WorkDate >= start && a.WorkDate <= end)
                .Select(a => new
                {
                    a.WorkDate,
                    a.ShiftTemplateId,
                    a.ShiftTemplate.StandardHours,
                    a.ShiftTemplate.StartTime,
                    a.ShiftTemplate.EndTime,
                })
                .ToListAsync(ct);

            var request = new WfhRequest
            {
                EmployeeInformationId = employee.Id,
                StartDate = start,
                EndDate = end,
                TotalDays = assignments.Select(a => a.WorkDate).Distinct().Count(),
                WorkPlan = "Xử lý công việc chuyên môn trên hệ thống, cập nhật tiến độ cuối ngày và tham gia họp trực tuyến theo lịch.",
                ApproverRoute = AttendanceEnums.OvertimeApproverRoute.Hr,
                Status = AttendanceEnums.WfhRequestStatus.Approved,
                Reason = reason,
                ReviewNote = "Đã duyệt; tính công theo đúng các ngày có phân ca.",
                ReviewedByUserId = adminId,
                ReviewedAt = Utc(start.AddDays(-2)),
                CreatedAt = Utc(start.AddDays(-5)),
                CreatedByUserId = employee.UserId,
            };
            await _context.WfhRequests.AddAsync(request, ct);
            await _context.SaveChangesAsync(ct);

            var existingKeys = (await _context.AttendanceRecords.AsNoTracking()
                .Where(a => a.EmployeeInformationId == employee.Id
                    && a.WorkDate >= start && a.WorkDate <= end
                    && a.ShiftTemplateId != null)
                .Select(a => new { a.WorkDate, ShiftTemplateId = a.ShiftTemplateId!.Value })
                .ToListAsync(ct))
                .Select(a => (a.WorkDate, a.ShiftTemplateId))
                .ToHashSet();

            var attendance = assignments
                .Where(a => !existingKeys.Contains((a.WorkDate, a.ShiftTemplateId)))
                .Select(a => new AttendanceRecord
                {
                    EmployeeInformationId = employee.Id,
                    WorkDate = a.WorkDate,
                    ShiftTemplateId = a.ShiftTemplateId,
                    Status = AttendanceEnums.AttendanceStatus.Present,
                    DayHours = a.StandardHours,
                    NightHours = 0m,
                    CheckInAt = a.WorkDate.ToDateTime(a.StartTime),
                    CheckOutAt = a.WorkDate.ToDateTime(a.EndTime),
                    Notes = $"WFH đã duyệt #{request.Id}; chấm công từ xa đủ theo ca Fulltime.",
                    CreatedAt = request.ReviewedAt!.Value,
                    CreatedByUserId = adminId,
                })
                .ToList();

            if (attendance.Count > 0)
            {
                await _context.AttendanceRecords.AddRangeAsync(attendance, ct);
                await _context.SaveChangesAsync(ct);
            }
        }

        _logger.LogInformation(
            "Seeded/checked {Count} WFH cases trong khoảng {Start:dd/MM/yyyy}–{End:dd/MM/yyyy}.",
            employees.Count, start.ToDateTime(TimeOnly.MinValue), end.ToDateTime(TimeOnly.MinValue));
    }
}
