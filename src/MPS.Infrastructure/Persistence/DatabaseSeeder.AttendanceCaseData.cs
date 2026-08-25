using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Entities.Evaluation;
using AttendanceEnums = MPS.Domain.Enums.Attendance;
using EvaluationEnums = MPS.Domain.Enums.Evaluation;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// Chấm công "có chuyện" cho một nhóm nhân sự được chọn: tăng ca ngày thường / thứ Bảy / Chủ nhật /
/// ngày lễ, nghỉ phép có lương / không lương / thai sản, đi muộn – về sớm – vắng – nửa công, và khối lượng giờ của hợp
/// đồng khoán việc.
///
/// <para><b>Vì sao cần.</b> Chấm công nền do <c>PreparePayrollMonthDataAsync</c> sinh là 8 giờ,
/// <c>Present</c>, thứ Hai–thứ Sáu, 08:00–17:00 cho TẤT CẢ mọi người. Với dữ liệu đó thì cột tăng
/// ca, phụ trội loại ngày, ngày nghỉ có lương và toàn bộ khối hỗ trợ theo tiến độ
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

    private async Task SeedAttendanceCasesAsync(CancellationToken ct)
    {
        var adminId = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId).Select(u => u.Id).FirstOrDefaultAsync(ct);
        var shiftId = await _context.ShiftTemplates.AsNoTracking()
            .Where(s => s.IsActive && s.Code == "FULLTIME")
            .Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        if (adminId == 0 || shiftId is null)
        {
            _logger.LogWarning("SeedAttendanceCases: thiếu admin hoặc mẫu ca; bỏ qua.");
            return;
        }

        // ⭐ X1a — "director" có mặt trong danh sách này để chấm công của Giám đốc do lớp case dựng
        // (có ngày nghỉ ốm), thay vì để `PreparePayrollMonthDataAsync` điền đủ công cho cả tháng.
        // Hai đường loại trừ nhau: lớp prepare chỉ điền khi nhân viên CHƯA có bản ghi nào trong kỳ.
        //
        // Giám đốc là người DUY NHẤT của công ty có mức lương vượt trần đóng BHXH (GD-4 = 55tr so với
        // trần 46,8tr / 50,6tr), nên case "vượt trần × thiếu công" không có chỗ nào khác để đặt. Việc
        // duyệt đơn trong dữ liệu demo đều do tài khoản admin thực hiện nên kịch bản duyệt không đổi.
        var usernames = new[]
        {
            "totruong_x1", "totruong_x2", "totruong_x3",
            "quandoc_x1", "quandoc_x2",
            "cn_x1_3", "cn_x1_4", "cn_x2_3", "cn_x2_4", "cn_x2_5", "cn_x3_5", "cn_x3_6",
            "director",
        };
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
        var maternityFrom = DemoDay(2, 1);
        await EnsureLeaveRequestAsync(emps, "cn_x1_4", "MATERNITY", maternityFrom, maternityFrom.AddMonths(6).AddDays(-1),
            isPaid: false, "Nghỉ chế độ thai sản 6 tháng — quỹ BHXH chi trả, doanh nghiệp không trả lương những ngày này.",
            leaveTypeIds, adminId, ct);
        await EnsureLeaveRequestAsync(emps, "cn_x2_4", "ANNUAL", DemoDay(2, 8), DemoDay(2, 10),
            isPaid: true, "Nghỉ phép năm về quê giỗ họ — hưởng nguyên lương, trừ quỹ phép.",
            leaveTypeIds, adminId, ct);
        await EnsureLeaveRequestAsync(emps, "cn_x2_4", "UNPAID", DemoDay(2, 22), DemoDay(2, 23),
            isPaid: false, "Nghỉ không hưởng lương do việc gia đình, đã hết quỹ phép trong tháng.",
            leaveTypeIds, adminId, ct);
        await EnsureLeaveRequestAsync(emps, "cn_x3_5", "ANNUAL", DemoDay(2, 6), DemoDay(2, 7),
            isPaid: true, "Nghỉ phép năm hai ngày để giải quyết việc gia đình; bàn giao ca và công việc đầy đủ.",
            leaveTypeIds, adminId, ct);
        await EnsureLeaveRequestAsync(emps, "cn_x3_6", "ANNUAL", DemoDay(2, 27), DemoDay(2, 27),
            isPaid: true, "Nghỉ phép năm một ngày để làm thủ tục hành chính cá nhân.",
            leaveTypeIds, adminId, ct);

        // ⭐ X1a — NGHỈ ỐM 14 NGÀY LÀM VIỆC của người có lương TRÊN TRẦN đóng BHXH (Giám đốc, GD-4 =
        // 55tr sau phụ lục X1b; trần kỳ 06/2026 = 46,8tr). Ghép đúng hai trục mà bộ seed cũ luôn tách
        // rời nhau: "vượt trần" và "không đủ công".
        //
        // Vì sao lộ lỗi. Khối bảo hiểm tính
        // MIN(MUCLUONG × công/công_chuẩn, trần) × tỷ_lệ — nền bị CHIA TRƯỚC rồi mới gặp trần,
        // nên với người nghỉ nhiều ngày thì trần gần như không bao giờ chạm tới. Đúng luật: trần chặn
        // mức lương THÁNG, chia theo ngày là bước sau.
        //
        // Số kỳ vọng (công chuẩn 06/2026 = 22 ngày, nghỉ 14 ⇒ còn 8 công):
        // engine lấy nền 55.000.000 × 8/22 = 20.000.000đ (không chạm trần);
        // đúng phải là MIN(55.000.000; 46.800.000) × 8/22 = 17.018.182đ.
        // Chênh nền 2.981.818đ ⇒ BHXH NLĐ 8% thu thừa 238.545đ, DN 17,5% (gộp HT 14% +
        // ỐĐ-TS 3% + TNLĐ-BNN 0,5%) thừa 521.818đ.
        //
        // ⚠️ CẬP NHẬT 24/08/2026 — case B3 (nghỉ ≥ 14 ngày thì tháng đó KHÔNG đóng BHXH, QĐ 595
        // Đ.42) ĐÃ ĐƯỢC CÀI: xem `PayrollDataCollector` (khoá `attendance.mienBhThieuNgayCong`) và
        // `PayrollCollectorInsuranceMinDaysTests`. Hệ quả với chính bộ dữ liệu này: Giám đốc chỉ còn 8
        // ngày công ⇒ kỳ 06/2026 của NV này nay MIỄN toàn bộ bảo hiểm, nên nó không còn minh hoạ
        // được trục "vượt trần + thiếu công" ở trên nữa — các con số kỳ vọng trong khối ghi chú này
        // giờ chỉ còn đúng ở tầng đơn vị (`InsuranceCapOrderTests`, nơi nạp thừa số trực tiếp).
        // Muốn giữ cả hai trục trên dữ liệu seed thì phải TÁCH thành hai NV: một người trên trần nghỉ
        // ÍT ngày (≥ 14 ngày công ⇒ vẫn đóng, minh hoạ trần), một người nghỉ DÀI (minh hoạ miễn).
        //
        // ⚠️ Khoảng 08→25 của kỳ giữa cho đúng 14 ngày làm việc với trục demo mặc định (06/2026 bắt
        // đầu vào thứ Hai). Đổi `Seed:DemoAnchorMonth` thì số ngày lệch đi — case vẫn còn ý nghĩa
        // (nghỉ dài ngày + trên trần) nhưng các con số kỳ vọng ở trên phải tính lại.
        await EnsureLeaveRequestAsync(emps, "director", "SICK", DemoDay(2, 8), DemoDay(2, 28),
            isPaid: false, "Nghỉ ốm dài ngày có giấy ra viện — hưởng chế độ ốm đau do quỹ BHXH chi trả.",
            leaveTypeIds, adminId, ct);

        // ⭐ BH13/BH14 — cặp biên đúng theo SỐ NGÀY NGHỈ KHÔNG HƯỞNG LƯƠNG mà nghiệp vụ cần đối
        // chiếu. Hai quản đốc có cùng loại HĐ, cùng ca, cùng nhóm phụ cấp; chỉ khác đúng 1 ngày nghỉ:
        // 13 ngày (01-17/07) và 14 ngày (01-20/07). Nhờ vậy có thể nhìn thẳng xem hệ thống cắt BH ở
        // đúng mốc hay đang suy luận nhầm từ số ngày còn đi làm.
        await EnsureLeaveRequestAsync(emps, "quandoc_x1", "SICK", DemoDay(2, 1), DemoDay(2, 17),
            isPaid: false, "Điều trị phục hồi sau phẫu thuật, nghỉ đúng 13 ngày làm việc có hồ sơ y tế.",
            leaveTypeIds, adminId, ct);
        await EnsureLeaveRequestAsync(emps, "quandoc_x2", "SICK", DemoDay(2, 1), DemoDay(2, 20),
            isPaid: false, "Điều trị nội trú và phục hồi, nghỉ đúng 14 ngày làm việc có hồ sơ y tế.",
            leaveTypeIds, adminId, ct);

        await SeedCaseMonthAttendanceAsync(emps, holidays, shiftId.Value, adminId, ct);
        await SeedOvertimeCasesAsync(emps, adminId, ct);
        await SeedLateEarlyAndAdjustmentCasesAsync(emps, adminId, ct);
        await SeedNamedEvaluationScoresAsync(adminId, ct);
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

                await AddMonthAttendanceAsync(empId, monthStart, holidays, includeSaturday, mine,
                    shiftId, adminId, ct);
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
        if (periodIndex != 2) return result;   // toàn bộ kịch bản "tháng xấu" đặt ở kỳ demo chính 07/2026

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
            // ⭐ CONG-HOUR-2026-08 — ngày thiếu giờ mà KHÔNG có đơn nào: chốt 5,5/8 giờ ⇒ 0,69 công.
            // Cố ý để `Status = Present` (không phải `Late`) vì vòng seed đơn từ bên dưới tự sinh đơn
            // Approved cho MỌI ngày Late/EarlyLeave — để `Late` thì ngày này lại được miễn trọn công
            // và kịch bản "làm thiếu giờ thì mất công theo tỷ lệ" không còn dữ liệu nào minh hoạ.
            (22, new(AttendanceEnums.AttendanceStatus.Present,
                     new TimeOnly(10, 30), ShiftEnd, 5.5m,
                     "Đi muộn 2,5 tiếng không có đơn — chỉ được tính 0,69 công")),
        };

        // Dồn về ngày làm việc gần nhất có thể làm hai mốc trùng nhau (khi trục demo đổi) — giữ mốc
        // đầu tiên thay vì để bộ khởi tạo từ điển ném khoá trùng và chặn khởi động app.
        var days = new Dictionary<DateOnly, SpecialDay>();
        foreach (var (day, spec) in badMonth) days.TryAdd(NearestWorkday(DemoDay(2, day)), spec);

        result["cn_x1_3"] = days;

        // ⭐ REST-RESP — giờ làm THƯỜNG vào Chủ nhật (không phải OT) của tổ trưởng có phụ cấp trách
        // nhiệm IsBhxhBase. Đây là nhánh còn thiếu trong ma trận cũ: phụ trội ngày nghỉ phải dùng đơn
        // giá giờ gồm cả phụ cấp lương gắn với công việc.
        var sunday = DemoDay(2, 12);
        while (sunday.DayOfWeek != DayOfWeek.Sunday) sunday = sunday.AddDays(1);
        result["totruong_x2"] = new Dictionary<DateOnly, SpecialDay>
        {
            [sunday] = new(AttendanceEnums.AttendanceStatus.Present, ShiftStart, ShiftEnd, 8m,
                "Ca điều phối kiểm kê Chủ nhật theo kế hoạch sản xuất, không phải đơn tăng ca."),
        };
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

            // Ngày cuối tuần chỉ được sinh khi có lịch nghiệp vụ rõ ràng: HĐ khoán làm thứ Bảy hoặc
            // một SpecialDay đã khai. Nhờ đó ca Chủ nhật REST-RESP có đủ attendance rồi lớp schedule
            // sẽ tạo EmployeeShiftAssignment tương ứng, không phải một dòng công rơi tự do.
            var isWorkday = day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday
                || (includeSaturday && day.DayOfWeek == DayOfWeek.Saturday)
                || specials.ContainsKey(day);
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

            await _context.AttendanceRecords.AddAsync(new AttendanceRecord
            {
                EmployeeInformationId = empId,
                WorkDate = day,
                ShiftTemplateId = shiftId,
                DayHours = hours,
                NightHours = 0m,
                Status = status,
                CheckInAt = checkIn is TimeOnly ci ? day.ToDateTime(ci) : null,
                CheckOutAt = checkOut is TimeOnly co ? day.ToDateTime(co) : null,
                Notes = onLeave ? "Nghỉ theo đơn đã duyệt" : special?.Note,
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

    // ===== Tăng ca ban ngày: ngày thường / thứ Bảy / Chủ nhật / ngày lễ =====
    /// <summary>Ngày mà ca tăng ca phải rơi vào — quyết định hệ số 150 / 200 / 300%.</summary>
    private enum OvertimeDayKind
    {
        /// <summary>Ngày làm việc thường (150%). Lệch cuối tuần thì đẩy sang ngày làm việc gần nhất.</summary>
        Workday,
        /// <summary>Thứ Bảy ngoài lịch Fulltime (200%).</summary>
        Saturday,
        /// <summary>Chủ nhật ngoài lịch Fulltime (200%).</summary>
        Sunday,
        /// <summary>Ngày nghỉ lễ (300%) — dùng đúng ngày đã ghi.</summary>
        Holiday,
    }

    private sealed record OvertimeCase(
        int PeriodIndex, int Day, OvertimeDayKind DayKind, TimeOnly Start, TimeOnly End, string Reason);

    private async Task SeedOvertimeCasesAsync(
        IReadOnlyDictionary<string, (int Id, int? UserId)> emps, int adminId, CancellationToken ct)
    {
        var byPerson = new List<(string Username, List<OvertimeCase> Cases)>();

        if (emps.ContainsKey("totruong_x1"))
            byPerson.Add(("totruong_x1", new List<OvertimeCase>
            {
                new(2, 9, OvertimeDayKind.Workday, new TimeOnly(17, 30), new TimeOnly(19, 30),
                    "Hoàn thiện đóng gói lô hàng giao sáng hôm sau"),
            }));

        if (emps.ContainsKey("totruong_x2"))
            byPerson.Add(("totruong_x2", new List<OvertimeCase>
            {
                new(2, 18, OvertimeDayKind.Saturday, new TimeOnly(8, 0), new TimeOnly(12, 0),
                    "Kiểm kê bán thành phẩm sáng thứ Bảy theo kế hoạch đã duyệt"),
            }));

        if (emps.ContainsKey("cn_x3_5"))
            byPerson.Add(("cn_x3_5", new List<OvertimeCase>
            {
                new(2, 12, OvertimeDayKind.Sunday, new TimeOnly(8, 0), new TimeOnly(12, 0),
                    "Xử lý lô sơn cần giao gấp vào đầu tuần"),
            }));

        if (emps.ContainsKey("cn_x3_6"))
            byPerson.Add(("cn_x3_6", new List<OvertimeCase>
            {
                new(0, 1, OvertimeDayKind.Holiday, new TimeOnly(8, 0), new TimeOnly(12, 0),
                    "Trực xử lý đơn hàng xuất khẩu ngày Quốc tế Lao động"),
                new(2, 22, OvertimeDayKind.Workday, new TimeOnly(17, 30), new TimeOnly(19, 30),
                    "Hoàn thiện kiểm tra chất lượng trước khi bàn giao"),
            }));

        var added = 0;
        foreach (var (username, cases) in byPerson)
            added += await SeedOvertimeCasesForAsync(emps[username], cases, adminId, ct);

        // ⭐ OT0 — đơn đã Approved nhưng chưa có AttendanceRecord ca OT: collector phải trả 0 đồng
        // cho tới khi có bằng chứng giờ vào/ra thật. Ca chính của người này vẫn đầy đủ và có phân ca.
        if (emps.TryGetValue("totruong_x3", out var noAttendance))
            added += await SeedApprovedOvertimeWithoutAttendanceAsync(noAttendance, adminId, ct);

        if (added > 0)
            _logger.LogInformation("Seeded {Count} ca tăng ca ban ngày (ngày thường / thứ Bảy / Chủ nhật / ngày lễ).", added);
    }

    /// <summary>
    /// Ghi đơn tăng ca đã duyệt + bản ghi chấm công của chính ca đó cho MỘT người.
    /// Idempotent theo (nhân viên, ngày làm việc).
    /// </summary>
    private async Task<int> SeedOvertimeCasesForAsync(
        (int Id, int? UserId) leader,
        IReadOnlyList<OvertimeCase> cases,
        int adminId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var added = 0;
        foreach (var c in cases)
        {
            var monthStart = DemoPeriodStart(c.PeriodIndex);
            var day = ResolveOvertimeDay(c, monthStart);

            if (await _context.OvertimeRequests.AsNoTracking()
                    .AnyAsync(o => o.EmployeeInformationId == leader.Id && o.WorkDate == day, ct)) continue;

            var dayHours = decimal.Round((decimal)(c.End - c.Start).TotalHours, 2);
            var request = new OvertimeRequest
            {
                EmployeeInformationId = leader.Id,
                WorkDate = day,
                OtStartTime = c.Start,
                OtEndTime = c.End,
                OtHours = dayHours,
                OtDayHours = dayHours,
                OtNightHours = 0m,
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
            var checkOut = day.ToDateTime(c.End);
            await _context.AttendanceRecords.AddAsync(new AttendanceRecord
            {
                EmployeeInformationId = leader.Id,
                WorkDate = day,
                OvertimeRequestId = request.Id,
                ShiftTemplateId = null,
                DayHours = dayHours,
                NightHours = 0m,
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

        return added;
    }

    private async Task<int> SeedApprovedOvertimeWithoutAttendanceAsync(
        (int Id, int? UserId) employee, int adminId, CancellationToken ct)
    {
        var day = NearestWorkday(DemoDay(2, 16));
        if (await _context.OvertimeRequests.AsNoTracking()
                .AnyAsync(o => o.EmployeeInformationId == employee.Id && o.WorkDate == day, ct))
            return 0;

        await _context.OvertimeRequests.AddAsync(new OvertimeRequest
        {
            EmployeeInformationId = employee.Id,
            WorkDate = day,
            OtStartTime = new TimeOnly(18, 0),
            OtEndTime = new TimeOnly(20, 0),
            OtHours = 2m,
            OtDayHours = 2m,
            OtNightHours = 0m,
            Status = AttendanceEnums.OtRequestStatus.Approved,
            ApproverRoute = AttendanceEnums.OvertimeApproverRoute.DepartmentManager,
            Reason = "Duyệt tăng ca chuẩn bị vật tư nhưng chưa ghi nhận giờ làm thực tế.",
            ReviewNote = "Đã duyệt kế hoạch; chờ chấm công ca tăng ca để thanh toán.",
            ReviewedByUserId = adminId,
            ReviewedAt = Utc(day.AddDays(-1)),
            ApproverReviewedByUserId = adminId,
            ApproverReviewedAt = Utc(day.AddDays(-1)),
            CreatedAt = Utc(day.AddDays(-3)),
            CreatedByUserId = adminId,
        }, ct);
        await _context.SaveChangesAsync(ct);
        return 1;
    }

    /// <summary>Quy ngày tăng ca về đúng loại ngày mà kịch bản cần (quyết định hệ số 150/200/300%).</summary>
    private static DateOnly ResolveOvertimeDay(OvertimeCase c, DateOnly monthStart)
    {
        var day = monthStart.AddDays(c.Day - 1);
        if (c.DayKind == OvertimeDayKind.Holiday) return day;      // ngày lễ là mốc cố định, không dời
        if (c.DayKind == OvertimeDayKind.Workday) return NearestWorkday(day);

        var target = c.DayKind == OvertimeDayKind.Saturday ? DayOfWeek.Saturday : DayOfWeek.Sunday;
        while (day.DayOfWeek != target) day = day.AddDays(1);
        return day;
    }

    // ===== Đơn đi muộn / về sớm + phiếu điều chỉnh chấm công =====
    private async Task SeedLateEarlyAndAdjustmentCasesAsync(
        IReadOnlyDictionary<string, (int Id, int? UserId)> emps, int adminId, CancellationToken ct)
    {
        if (!emps.TryGetValue("cn_x1_3", out var emp)) return;

        var specials = BuildSpecialDays(2).GetValueOrDefault("cn_x1_3");
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

    // ===== Điểm đánh giá có chủ đích cho các case mới =====
    private async Task SeedNamedEvaluationScoresAsync(int adminId, CancellationToken ct)
    {
        var period = DemoPeriodStart(2);
        var targetScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["cn_x1_3"] = 2,       // tháng xấu -> hệ số 0,5
            ["quandoc_x1"] = 4,    // cặp biên nghỉ 13 ngày
            ["quandoc_x2"] = 4,    // cặp biên nghỉ 14 ngày
            ["quandoc_x3"] = 5,    // WFH tháng 7 -> hệ số 1,2
            ["totruong_x2"] = 4,   // làm ca thường ngày nghỉ
            ["totruong_x3"] = 4,   // OT duyệt nhưng chưa chấm
            ["cn_x3_6"] = 3,       // lương 10 triệu + ngưỡng cơm -> hệ số 0,8
            ["cn_x3_5"] = 4,       // phép năm + OT Chủ nhật + độc hại -> hệ số 1,0
            ["accountant"] = 4,    // lương 30 triệu + NPT hết hiệu lực -> hệ số 1,0
        };

        var userIds = await _context.Users.AsNoTracking()
            .Where(u => targetScores.Keys.Contains(u.Username))
            .ToDictionaryAsync(u => u.Id, u => u.Username, ct);
        if (userIds.Count == 0) return;

        var evaluations = await _context.MonthlyEvaluations
            .Include(m => m.Scores)
            .Include(m => m.SourceLinks)
                .ThenInclude(l => l.OrderEvaluationSubject)
                    .ThenInclude(s => s!.Scores)
            .Where(m => userIds.Keys.Contains(m.EmployeeUserId)
                && m.PeriodYear == period.Year && m.PeriodMonth == period.Month)
            .ToListAsync(ct);

        foreach (var evaluation in evaluations)
        {
            var score = targetScores[userIds[evaluation.EmployeeUserId]];
            evaluation.FinalScore = score;
            evaluation.Status = EvaluationEnums.EvaluationStatus.Approved;
            evaluation.ApprovedByUserId = adminId;
            evaluation.ApprovedAt ??= DateTime.UtcNow;

            foreach (var line in evaluation.Scores) line.Score = score;
            foreach (var link in evaluation.SourceLinks)
            {
                link.ContributedScore = score;
                if (link.OrderEvaluationSubject is not { } subject) continue;
                subject.FinalScore = score;
                foreach (var line in subject.Scores) line.Score = score;
            }
        }

        await _context.SaveChangesAsync(ct);
    }
}
