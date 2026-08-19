using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Entities.Evaluation;
using AttendanceEnums = MPS.Domain.Enums.Attendance;
using EvaluationEnums = MPS.Domain.Enums.Evaluation;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// Chấm công "có chuyện" cho một nhóm nhân sự được chọn: tăng ca đủ bốn loại ngày, nghỉ phép có
/// lương / không lương / thai sản, đi muộn – về sớm – vắng – nửa công, và khối lượng giờ của hợp
/// đồng khoán việc.
///
/// <para><b>Vì sao cần.</b> Chấm công nền do <c>PreparePayrollMonthDataAsync</c> sinh là 8 giờ,
/// <c>Present</c>, thứ Hai–thứ Sáu, 08:00–17:00 cho TẤT CẢ mọi người. Với dữ liệu đó thì cột tăng
/// ca, phụ trội loại ngày, phụ trội ca đêm, ngày nghỉ có lương và toàn bộ khối hỗ trợ theo tiến độ
/// của hợp đồng khoán đều bằng 0 trên mọi dòng lương.</para>
///
/// <para><b>Hai ràng buộc đã tính tới:</b>
/// <list type="number">
///   <item>Bản ghi chấm công của CA TĂNG CA là bản ghi RIÊNG (<c>OvertimeRequestId</c> khác null,
///   <c>ShiftTemplateId</c> null) — gộp giờ vào ca chính sẽ bị tính ba lần (chú thích A1 trong
///   <c>PayrollDataCollector</c>).</item>
///   <item>Tiền tăng ca chỉ phát sinh khi có ĐỦ CẶP: đơn đã duyệt <b>và</b> bản ghi chấm công ca đó
///   có giờ vào/ra. Đơn duyệt suông không ra tiền.</item>
/// </list></para>
///
/// <para>Phải chạy TRƯỚC <c>SeedPayrollAsync</c>: bước chuẩn bị của payroll chỉ sinh chấm công nền
/// cho người CHƯA có bản ghi nào trong tháng, nên seed trước là giữ nguyên được kịch bản ở đây.</para>
///
/// <para>Idempotent theo (nhân viên, tháng) với chấm công và theo (nhân viên, ngày) với đơn từ.</para>
/// </summary>
public sealed partial class DatabaseSeeder
{
    /// <summary>Một ngày đặc biệt trong tháng — đè lên ca chính tiêu chuẩn.</summary>
    private sealed record SpecialDay(
        AttendanceEnums.AttendanceStatus Status,
        TimeOnly? CheckIn = null,
        TimeOnly? CheckOut = null,
        decimal DayHours = 8m,
        string? Note = null);

    private static readonly TimeOnly ShiftStart = new(8, 0);
    private static readonly TimeOnly ShiftEnd = new(17, 0);

    /// <summary>Ca đêm mẫu <c>CA-DEM</c> — trọn 8 giờ nằm trong khung đêm 22:00–06:00 (Điều 106 BLLĐ 2019).</summary>
    private static readonly TimeOnly NightShiftStart = new(22, 0);
    private static readonly TimeOnly NightShiftEnd = new(6, 0);

    private async Task SeedAttendanceCasesAsync(CancellationToken ct)
    {
        var adminId = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId).Select(u => u.Id).FirstOrDefaultAsync(ct);
        var shiftId = await _context.ShiftTemplates.AsNoTracking()
            .Where(s => s.IsActive).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        var nightShiftId = await _context.ShiftTemplates.AsNoTracking()
            .Where(s => s.IsActive && s.Code == "CA-DEM").Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        if (adminId == 0 || shiftId is null)
        {
            _logger.LogWarning("SeedAttendanceCases: thiếu admin hoặc mẫu ca; bỏ qua.");
            return;
        }

        var usernames = new[] { "totruong_x1", "cn_x1_3", "cn_x1_4", "cn_x2_3", "cn_x2_4", "cn_x2_5", "cn_x3_5" };
        var emps = await _context.EmployeeInformations.AsNoTracking()
            .Where(e => e.User != null && usernames.Contains(e.User.Username))
            .Select(e => new { e.Id, e.UserId, Username = e.User!.Username })
            .ToDictionaryAsync(x => x.Username, x => (x.Id, x.UserId), StringComparer.OrdinalIgnoreCase, ct);
        if (emps.Count == 0)
        {
            _logger.LogDebug("SeedAttendanceCases: chưa có hồ sơ nhân sự demo; bỏ qua.");
            return;
        }

        var holidays = await LoadHolidayDatesAsync(ct);
        var leaveTypeIds = await _context.LeaveTypes.AsNoTracking()
            .ToDictionaryAsync(t => t.Code, t => t.Id, StringComparer.OrdinalIgnoreCase, ct);

        // Nghỉ dài ngày + đơn từ phải có TRƯỚC khi dựng chấm công, vì trạng thái ngày công của
        // những ngày đó suy ra từ chính các đơn này.
        var maternityFrom = DemoDay(1, 15);
        await EnsureLeaveRequestAsync(emps, "cn_x1_4", "MATERNITY", maternityFrom, maternityFrom.AddMonths(6).AddDays(-1),
            isPaid: false, "Nghỉ chế độ thai sản 6 tháng — quỹ BHXH chi trả, doanh nghiệp không trả lương những ngày này.",
            leaveTypeIds, adminId, ct);
        await EnsureLeaveRequestAsync(emps, "cn_x2_4", "ANNUAL", DemoDay(1, 8), DemoDay(1, 10),
            isPaid: true, "Nghỉ phép năm về quê giỗ họ — hưởng nguyên lương, trừ quỹ phép.",
            leaveTypeIds, adminId, ct);
        await EnsureLeaveRequestAsync(emps, "cn_x2_4", "UNPAID", DemoDay(1, 22), DemoDay(1, 23),
            isPaid: false, "Nghỉ không hưởng lương do việc gia đình, đã hết quỹ phép trong tháng.",
            leaveTypeIds, adminId, ct);

        await SeedCaseMonthAttendanceAsync(emps, holidays, shiftId.Value, nightShiftId, adminId, ct);
        await SeedOvertimeCasesAsync(emps, adminId, ct);
        await SeedLateEarlyAndAdjustmentCasesAsync(emps, adminId, ct);
        await SeedLowScoreEvaluationAsync(emps, adminId, ct);
    }

    private async Task<HashSet<DateOnly>> LoadHolidayDatesAsync(CancellationToken ct)
    {
        var ranges = await _context.PublicHolidays.AsNoTracking()
            .Where(h => h.IsActive)
            .Select(h => new { h.HolidayDate, h.HolidayEndDate })
            .ToListAsync(ct);

        var days = new HashSet<DateOnly>();
        foreach (var r in ranges)
            for (var d = r.HolidayDate; d <= r.HolidayEndDate; d = d.AddDays(1))
                days.Add(d);
        return days;
    }

    // ===== Chấm công cả tháng cho từng nhân vật =====
    private async Task SeedCaseMonthAttendanceAsync(
        IReadOnlyDictionary<string, (int Id, int? UserId)> emps,
        IReadOnlySet<DateOnly> holidays,
        int shiftId,
        int? nightShiftId,
        int adminId,
        CancellationToken ct)
    {
        for (var index = 0; index < PayrollDemoMonths; index++)
        {
            var monthStart = DemoPeriodStart(index);
            var specials = BuildSpecialDays(index);

            foreach (var username in emps.Keys)
            {
                var empId = emps[username].Id;
                var already = await _context.AttendanceRecords.AsNoTracking()
                    .AnyAsync(a => a.EmployeeInformationId == empId
                        && a.WorkDate >= monthStart && a.WorkDate <= EndOfMonth(monthStart), ct);
                if (already) continue;

                // HĐ khoán có khối lượng giao theo lô nên làm cả thứ Bảy — đây là thứ đẩy tổng giờ
                // vượt mốc 200h của bậc hỗ trợ theo tiến độ; người làm công ăn lương thì không.
                var includeSaturday = username.Equals("cn_x2_5", StringComparison.OrdinalIgnoreCase);
                var mine = specials.GetValueOrDefault(username, new Dictionary<DateOnly, SpecialDay>());

                // ⭐ Thợ trực lò sấy chạy CA ĐÊM thường xuyên — nguồn duy nhất của phụ cấp làm đêm 30%
                // cho giờ THƯỜNG (khác hẳn tăng ca ban đêm, vốn đi qua đường hệ số OT).
                var nightWorker = username.Equals("cn_x3_5", StringComparison.OrdinalIgnoreCase);
                var shiftForPerson = nightWorker && nightShiftId is int nightId ? nightId : shiftId;

                await AddMonthAttendanceAsync(empId, monthStart, holidays, includeSaturday, mine,
                    shiftForPerson, adminId, nightWorker, ct);
            }
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded chấm công chi tiết {Months} tháng cho {People} nhân sự case.",
            PayrollDemoMonths, emps.Count);
    }

    /// <summary>Ngày đặc biệt của từng người trong kỳ <paramref name="periodIndex"/>.</summary>
    private Dictionary<string, Dictionary<DateOnly, SpecialDay>> BuildSpecialDays(int periodIndex)
    {
        var result = new Dictionary<string, Dictionary<DateOnly, SpecialDay>>(StringComparer.OrdinalIgnoreCase);
        if (periodIndex != 1) return result;   // toàn bộ kịch bản "tháng xấu" đặt ở kỳ giữa

        // ⭐ C12 — một tháng có đủ mọi sự cố chấm công.
        var badMonth = new (int Day, SpecialDay Spec)[]
        {
            (4,  new(AttendanceEnums.AttendanceStatus.Late,
                     new TimeOnly(8, 47), ShiftEnd, 8m, "Đi muộn 47 phút — có đơn được duyệt")),
            (12, new(AttendanceEnums.AttendanceStatus.Late,
                     new TimeOnly(8, 32), ShiftEnd, 8m, "Đi muộn 32 phút — có đơn được duyệt")),
            (17, new(AttendanceEnums.AttendanceStatus.EarlyLeave,
                     ShiftStart, new TimeOnly(15, 30), 8m, "Về sớm 90 phút đưa con đi khám")),
            (19, new(AttendanceEnums.AttendanceStatus.Absent,
                     null, null, 0m, "Nghỉ không báo trước, không có đơn")),
            (25, new(AttendanceEnums.AttendanceStatus.HalfDay,
                     ShiftStart, new TimeOnly(12, 0), 4m, "Làm nửa ca buổi sáng")),
            (26, new(AttendanceEnums.AttendanceStatus.Present,
                     new TimeOnly(9, 15), ShiftEnd, 8m, "Máy chấm công lỗi — đã có phiếu điều chỉnh được duyệt")),
        };

        // Dồn về ngày làm việc gần nhất có thể làm hai mốc trùng nhau (khi trục demo đổi) — giữ mốc
        // đầu tiên thay vì để bộ khởi tạo từ điển ném khoá trùng và chặn khởi động app.
        var days = new Dictionary<DateOnly, SpecialDay>();
        foreach (var (day, spec) in badMonth) days.TryAdd(NearestWorkday(DemoDay(1, day)), spec);

        result["cn_x1_3"] = days;
        return result;
    }

    /// <summary>Đẩy tới ngày làm việc gần nhất (không rơi vào cuối tuần) để kịch bản không mất tác dụng.</summary>
    private static DateOnly NearestWorkday(DateOnly day) => day.DayOfWeek switch
    {
        DayOfWeek.Saturday => day.AddDays(2),
        DayOfWeek.Sunday => day.AddDays(1),
        _ => day,
    };

    private async Task AddMonthAttendanceAsync(
        int empId,
        DateOnly monthStart,
        IReadOnlySet<DateOnly> holidays,
        bool includeSaturday,
        IReadOnlyDictionary<DateOnly, SpecialDay> specials,
        int shiftId,
        int adminId,
        bool nightShift,
        CancellationToken ct)
    {
        // Chỉ chấm công những ngày hợp đồng THỰC SỰ phủ — người vào làm hoặc nghỉ việc giữa tháng
        // không được có ngày công ngoài khoảng hiệu lực.
        var (from, to) = await LoadContractWindowAsync(empId, monthStart, ct);
        if (from is null) return;

        var leaveDays = await LoadApprovedLeaveDaysAsync(empId, monthStart, ct);
        var now = DateTime.UtcNow;
        var end = EndOfMonth(monthStart);

        for (var day = monthStart; day <= end; day = day.AddDays(1))
        {
            if (day < from || (to is DateOnly last && day > last)) continue;
            if (holidays.Contains(day)) continue;

            var isWorkday = day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday
                || (includeSaturday && day.DayOfWeek == DayOfWeek.Saturday);
            if (!isWorkday) continue;

            var special = specials.GetValueOrDefault(day);
            var onLeave = leaveDays.Contains(day);

            var status = onLeave
                ? AttendanceEnums.AttendanceStatus.OnLeave
                : special?.Status ?? AttendanceEnums.AttendanceStatus.Present;
            TimeOnly? checkIn = onLeave ? null : special?.CheckIn ?? ShiftStart;
            TimeOnly? checkOut = onLeave ? null : special?.CheckOut ?? ShiftEnd;
            var hours = onLeave ? 0m : special?.DayHours ?? 8m;
            if (status == AttendanceEnums.AttendanceStatus.Absent) { checkIn = null; checkOut = null; hours = 0m; }

            // Ca đêm 22:00–06:00: toàn bộ giờ nằm trong khung đêm (Điều 106) ⇒ đổ vào NightHours để
            // hưởng phụ cấp làm đêm 30% cho giờ THƯỜNG. Giờ ra rơi sang ngày hôm sau.
            if (nightShift && !onLeave && status != AttendanceEnums.AttendanceStatus.Absent)
            {
                checkIn = NightShiftStart;
                checkOut = NightShiftEnd;
            }

            await _context.AttendanceRecords.AddAsync(new AttendanceRecord
            {
                EmployeeInformationId = empId,
                WorkDate = day,
                ShiftTemplateId = shiftId,
                DayHours = nightShift ? 0m : hours,
                NightHours = nightShift ? hours : 0m,
                Status = status,
                CheckInAt = checkIn is TimeOnly ci ? day.ToDateTime(ci) : null,
                CheckOutAt = checkOut is TimeOnly co
                    ? (nightShift ? day.AddDays(1).ToDateTime(co) : day.ToDateTime(co))
                    : null,
                Notes = onLeave ? "Nghỉ theo đơn đã duyệt" : special?.Note ?? (nightShift ? "Ca đêm trực lò sấy" : null),
                CreatedAt = now,
                CreatedByUserId = adminId,
            }, ct);
        }
    }

    private async Task<(DateOnly? From, DateOnly? To)> LoadContractWindowAsync(
        int empId, DateOnly monthStart, CancellationToken ct)
    {
        var end = EndOfMonth(monthStart);
        var startDt = Utc(monthStart);
        var endDt = end.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var windows = await _context.Contracts.AsNoTracking()
            .Where(c => c.EmployeeInformationId == empId
                && c.Status != MPS.Domain.Enums.Contract.ContractStatus.Cancelled
                && c.Status != MPS.Domain.Enums.Contract.ContractStatus.Draft
                && c.EffectiveFrom <= endDt
                && (c.EffectiveTo == null || c.EffectiveTo >= startDt))
            .Select(c => new { c.EffectiveFrom, c.EffectiveTo })
            .ToListAsync(ct);
        if (windows.Count == 0) return (null, null);

        var from = windows.Min(w => DateOnly.FromDateTime(w.EffectiveFrom));
        var to = windows.Any(w => w.EffectiveTo == null)
            ? (DateOnly?)null
            : windows.Max(w => DateOnly.FromDateTime(w.EffectiveTo!.Value));
        return (from < monthStart ? monthStart : from, to);
    }

    private async Task<HashSet<DateOnly>> LoadApprovedLeaveDaysAsync(
        int empId, DateOnly monthStart, CancellationToken ct)
    {
        var end = EndOfMonth(monthStart);
        var requests = await _context.LeaveRequests.AsNoTracking()
            .Where(r => r.EmployeeInformationId == empId
                && r.Status == AttendanceEnums.LeaveRequestStatus.Approved
                && r.StartDate <= end && r.EndDate >= monthStart)
            .Select(r => new { r.StartDate, r.EndDate })
            .ToListAsync(ct);

        var days = new HashSet<DateOnly>();
        foreach (var r in requests)
            for (var d = r.StartDate; d <= r.EndDate; d = d.AddDays(1))
                if (d >= monthStart && d <= end) days.Add(d);
        return days;
    }

    // ===== Đơn nghỉ =====
    private async Task EnsureLeaveRequestAsync(
        IReadOnlyDictionary<string, (int Id, int? UserId)> emps,
        string username,
        string leaveTypeCode,
        DateOnly start,
        DateOnly end,
        bool isPaid,
        string reason,
        IReadOnlyDictionary<string, int> leaveTypeIds,
        int adminId,
        CancellationToken ct)
    {
        if (!emps.TryGetValue(username, out var emp)) return;
        if (!leaveTypeIds.TryGetValue(leaveTypeCode, out var typeId)) return;
        if (await _context.LeaveRequests.AsNoTracking()
                .AnyAsync(r => r.EmployeeInformationId == emp.Id && r.StartDate == start && r.LeaveTypeId == typeId, ct))
            return;

        var totalDays = 0m;
        for (var d = start; d <= end; d = d.AddDays(1))
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) totalDays++;

        await _context.LeaveRequests.AddAsync(new LeaveRequest
        {
            EmployeeInformationId = emp.Id,
            LeaveTypeId = typeId,
            StartDate = start,
            EndDate = end,
            TotalDays = totalDays,
            IsPaid = isPaid,
            Status = AttendanceEnums.LeaveRequestStatus.Approved,
            ApproverRoute = AttendanceEnums.OvertimeApproverRoute.Hr,
            Reason = reason,
            IsRetroactive = false,
            ReviewNote = "Đồng ý.",
            ReviewedByUserId = adminId,
            ReviewedAt = Utc(start.AddDays(-2)),
            CreatedAt = Utc(start.AddDays(-5)),
            CreatedByUserId = adminId,
        }, ct);
        await _context.SaveChangesAsync(ct);

        await ConsumeLeaveBalanceAsync(emp.Id, typeId, start.Year, totalDays, ct);
    }

    /// <summary>Trừ quỹ phép tương ứng để <c>UsedDays</c> không đứng yên ở 0 như trước.</summary>
    private async Task ConsumeLeaveBalanceAsync(int empId, int leaveTypeId, int year, decimal days, CancellationToken ct)
    {
        var balance = await _context.LeaveBalances
            .FirstOrDefaultAsync(b => b.EmployeeInformationId == empId && b.LeaveTypeId == leaveTypeId && b.Year == year, ct);
        if (balance is null) return;

        balance.UsedDays = Math.Min(balance.TotalDays, balance.UsedDays + days);
        balance.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    // ===== Tăng ca: đủ bốn loại ngày + ca vắt nửa đêm =====
    /// <summary>Ngày mà ca tăng ca phải rơi vào — quyết định hệ số 150 / 200 / 300%.</summary>
    private enum OvertimeDayKind
    {
        /// <summary>Ngày làm việc thường (150%). Lệch cuối tuần thì đẩy sang ngày làm việc gần nhất.</summary>
        Workday,
        /// <summary>Ngày nghỉ hằng tuần (200%) — ép về Chủ nhật.</summary>
        WeeklyRest,
        /// <summary>Ngày nghỉ lễ (300%) — dùng đúng ngày đã ghi.</summary>
        Holiday,
        /// <summary>Ngày cuối tháng — để ca đêm vắt sang ngày (và tháng) kế tiếp.</summary>
        MonthEnd,
    }

    private sealed record OvertimeCase(
        int PeriodIndex, int Day, OvertimeDayKind DayKind, TimeOnly Start, TimeOnly End, string Reason);

    private async Task SeedOvertimeCasesAsync(
        IReadOnlyDictionary<string, (int Id, int? UserId)> emps, int adminId, CancellationToken ct)
    {
        if (!emps.TryGetValue("totruong_x1", out var leader)) return;

        var cases = new List<OvertimeCase>
        {
            // Kỳ cũ nhất có 01/5 — tăng ca NGÀY LỄ hưởng ít nhất 300% (Điều 98 k.1 điểm c).
            new(0, 1, OvertimeDayKind.Holiday, new TimeOnly(8, 0), new TimeOnly(12, 0),
                "Chạy đơn xuất khẩu gấp trong ngày lễ 01/5"),
        };
        // Kỳ giữa và kỳ mới nhất có cùng bộ tăng ca ⇒ so được tác động của mốc luật 01/07/2026:
        // tiền tăng ca kỳ trước CHỊU thuế, kỳ sau được miễn toàn bộ.
        foreach (var period in new[] { 1, 2 })
        {
            cases.Add(new(period, 9, OvertimeDayKind.Workday, new TimeOnly(18, 0), new TimeOnly(20, 0),
                "Tăng ca ngày thường hoàn thiện lô hàng"));
            cases.Add(new(period, 14, OvertimeDayKind.Workday, new TimeOnly(18, 0), new TimeOnly(20, 30),
                "Tăng ca ngày thường bù tiến độ công đoạn ghép"));
            cases.Add(new(period, 21, OvertimeDayKind.WeeklyRest, new TimeOnly(8, 0), new TimeOnly(12, 0),
                "Tăng ca ngày nghỉ hằng tuần — kiểm hàng trước giao"));
            // Ca đêm VẮT NỬA ĐÊM ở ngày cuối tháng: phần giờ sau 00:00 rơi sang ngày (và có thể là
            // tháng) kế tiếp — nhánh tra hằng số luật lao động theo TỪNG NGÀY.
            cases.Add(new(period, 0, OvertimeDayKind.MonthEnd, new TimeOnly(22, 0), new TimeOnly(2, 0),
                "Trực lò sấy qua đêm cuối tháng"));
        }

        var now = DateTime.UtcNow;
        var added = 0;
        foreach (var c in cases)
        {
            var monthStart = DemoPeriodStart(c.PeriodIndex);
            var day = ResolveOvertimeDay(c, monthStart);

            if (await _context.OvertimeRequests.AsNoTracking()
                    .AnyAsync(o => o.EmployeeInformationId == leader.Id && o.WorkDate == day, ct)) continue;

            var (dayHours, nightHours) = SplitDayNight(c.Start, c.End);
            var request = new OvertimeRequest
            {
                EmployeeInformationId = leader.Id,
                WorkDate = day,
                OtStartTime = c.Start,
                OtEndTime = c.End,
                OtHours = dayHours + nightHours,
                OtDayHours = dayHours,
                OtNightHours = nightHours,
                Status = AttendanceEnums.OtRequestStatus.Approved,
                ApproverRoute = AttendanceEnums.OvertimeApproverRoute.DepartmentManager,
                Reason = c.Reason,
                ReviewNote = "Đồng ý, bố trí nghỉ bù nếu vượt hạn mức.",
                ReviewedByUserId = adminId,
                ReviewedAt = Utc(day.AddDays(-1)),
                ApproverReviewedByUserId = adminId,
                ApproverReviewedAt = Utc(day.AddDays(-1)),
                CreatedAt = Utc(day.AddDays(-3)),
                CreatedByUserId = adminId,
            };
            await _context.OvertimeRequests.AddAsync(request, ct);
            await _context.SaveChangesAsync(ct);

            // Bản ghi chấm công RIÊNG của ca tăng ca — không có giờ vào/ra thì đơn duyệt suông
            // không sinh đồng nào.
            var checkIn = day.ToDateTime(c.Start);
            var checkOut = c.End < c.Start
                ? day.AddDays(1).ToDateTime(c.End)      // ca vắt nửa đêm
                : day.ToDateTime(c.End);
            await _context.AttendanceRecords.AddAsync(new AttendanceRecord
            {
                EmployeeInformationId = leader.Id,
                WorkDate = day,
                OvertimeRequestId = request.Id,
                ShiftTemplateId = null,
                DayHours = dayHours,
                NightHours = nightHours,
                Status = AttendanceEnums.AttendanceStatus.Present,
                CheckInAt = checkIn,
                CheckOutAt = checkOut,
                Notes = "Chấm công ca tăng ca",
                CreatedAt = now,
                CreatedByUserId = adminId,
            }, ct);
            await _context.SaveChangesAsync(ct);
            added++;
        }

        if (added > 0) _logger.LogInformation("Seeded {Count} ca tăng ca (thường / nghỉ tuần / lễ / đêm).", added);
    }

    /// <summary>Quy ngày tăng ca về đúng loại ngày mà kịch bản cần (quyết định hệ số 150/200/300%).</summary>
    private static DateOnly ResolveOvertimeDay(OvertimeCase c, DateOnly monthStart)
    {
        if (c.DayKind == OvertimeDayKind.MonthEnd) return EndOfMonth(monthStart);

        var day = monthStart.AddDays(c.Day - 1);
        if (c.DayKind == OvertimeDayKind.Holiday) return day;      // ngày lễ là mốc cố định, không dời
        if (c.DayKind == OvertimeDayKind.Workday) return NearestWorkday(day);

        while (day.DayOfWeek != DayOfWeek.Sunday) day = day.AddDays(1);
        return day;
    }

    /// <summary>Tách giờ ngày / giờ đêm theo khung 22:00–06:00 (Điều 106 BLLĐ 2019).</summary>
    private static (decimal Day, decimal Night) SplitDayNight(TimeOnly start, TimeOnly end)
    {
        var total = end >= start
            ? (end - start).TotalHours
            : (TimeSpan.FromHours(24) - (start - end)).TotalHours;

        decimal night = 0m;
        for (var minute = 0; minute < (int)Math.Round(total * 60); minute++)
        {
            var at = start.AddMinutes(minute);
            if (at.Hour >= 22 || at.Hour < 6) night += 1m / 60m;
        }

        night = decimal.Round(night, 2);
        return (decimal.Round((decimal)total - night, 2), night);
    }

    // ===== Đơn đi muộn / về sớm + phiếu điều chỉnh chấm công =====
    private async Task SeedLateEarlyAndAdjustmentCasesAsync(
        IReadOnlyDictionary<string, (int Id, int? UserId)> emps, int adminId, CancellationToken ct)
    {
        if (!emps.TryGetValue("cn_x1_3", out var emp)) return;

        var specials = BuildSpecialDays(1).GetValueOrDefault("cn_x1_3");
        if (specials is null) return;

        foreach (var (day, spec) in specials)
        {
            var kind = spec.Status switch
            {
                AttendanceEnums.AttendanceStatus.Late => AttendanceEnums.LateEarlyKind.LateArrival,
                AttendanceEnums.AttendanceStatus.EarlyLeave => AttendanceEnums.LateEarlyKind.EarlyLeave,
                _ => (AttendanceEnums.LateEarlyKind?)null,
            };
            if (kind is null) continue;
            if (await _context.LateEarlyRequests.AsNoTracking()
                    .AnyAsync(r => r.EmployeeInformationId == emp.Id && r.WorkDate == day, ct)) continue;

            var minutes = kind == AttendanceEnums.LateEarlyKind.LateArrival
                ? (short)(spec.CheckIn!.Value - ShiftStart).TotalMinutes
                : (short)(ShiftEnd - spec.CheckOut!.Value).TotalMinutes;

            await _context.LateEarlyRequests.AddAsync(new LateEarlyRequest
            {
                EmployeeInformationId = emp.Id,
                WorkDate = day,
                Kind = kind.Value,
                Minutes = minutes,
                IsUnplanned = kind == AttendanceEnums.LateEarlyKind.LateArrival,
                DeductFromLeaveBalance = false,
                ApproverRoute = AttendanceEnums.OvertimeApproverRoute.DepartmentManager,
                Status = AttendanceEnums.LateEarlyRequestStatus.Approved,
                Reason = spec.Note,
                ReviewNote = "Đồng ý, nhắc nhở lần sau.",
                ReviewedByUserId = adminId,
                ReviewedAt = Utc(day),
                CreatedAt = Utc(day),
                CreatedByUserId = adminId,
            }, ct);
        }
        await _context.SaveChangesAsync(ct);

        // Phiếu điều chỉnh chấm công đã duyệt — ngày máy chấm công lỗi.
        var adjustDay = specials.FirstOrDefault(kv => kv.Value.Note != null && kv.Value.Note.Contains("điều chỉnh")).Key;
        if (adjustDay == default) return;

        var record = await _context.AttendanceRecords.AsNoTracking()
            .FirstOrDefaultAsync(a => a.EmployeeInformationId == emp.Id && a.WorkDate == adjustDay
                && a.OvertimeRequestId == null, ct);
        if (record is null) return;
        if (await _context.AttendanceAdjustments.AsNoTracking()
                .AnyAsync(a => a.AttendanceRecordId == record.Id, ct)) return;

        await _context.AttendanceAdjustments.AddAsync(new AttendanceAdjustment
        {
            AttendanceRecordId = record.Id,
            EmployeeInformationId = emp.Id,
            NewStatus = AttendanceEnums.AttendanceStatus.Present,
            NewCheckInAt = adjustDay.ToDateTime(ShiftStart),
            NewCheckOutAt = adjustDay.ToDateTime(ShiftEnd),
            Reason = "Máy chấm công cổng xưởng lỗi buổi sáng, tổ trưởng xác nhận có mặt đúng giờ.",
            Status = AttendanceEnums.AdjustmentStatus.Approved,
            ReviewNote = "Đã đối chiếu camera, chấp thuận.",
            ReviewedByUserId = adminId,
            ReviewedAt = Utc(adjustDay.AddDays(1)),
            OriginalStatus = record.Status,
            OriginalCheckInAt = record.CheckInAt,
            OriginalCheckOutAt = record.CheckOutAt,
            CreatedAt = Utc(adjustDay.AddDays(1)),
            CreatedByUserId = adminId,
        }, ct);
        await _context.SaveChangesAsync(ct);
    }

    // ===== Điểm đánh giá thấp cho nhân vật "tháng xấu" =====
    private async Task SeedLowScoreEvaluationAsync(
        IReadOnlyDictionary<string, (int Id, int? UserId)> emps, int adminId, CancellationToken ct)
    {
        if (!emps.TryGetValue("cn_x1_3", out var emp) || emp.UserId is not int userId) return;

        var period = DemoPeriodStart(1);
        if (await _context.MonthlyEvaluations.AsNoTracking()
                .AnyAsync(m => m.EmployeeUserId == userId && m.PeriodYear == period.Year
                    && m.PeriodMonth == period.Month, ct)) return;

        await _context.MonthlyEvaluations.AddAsync(new MonthlyEvaluation
        {
            EmployeeUserId = userId,
            PeriodYear = period.Year,
            PeriodMonth = period.Month,
            SubjectRoleIdSnapshot = SystemRoles.EmployeeId,
            Source = EvaluationEnums.EvaluationSource.Direct,
            // Rơi vào bậc thấp nhất của bảng hệ số năng suất (0.5) — hệ quả trực tiếp của tháng có
            // vắng không phép, đi muộn và nửa công ở trên.
            FinalScore = 2.20m,
            Status = EvaluationEnums.EvaluationStatus.Approved,
            ApprovedByUserId = adminId,
            ApprovedAt = DateTime.UtcNow,
            CreatedByUserId = adminId,
            CreatedAt = DateTime.UtcNow,
        }, ct);
        await _context.SaveChangesAsync(ct);
    }
}
