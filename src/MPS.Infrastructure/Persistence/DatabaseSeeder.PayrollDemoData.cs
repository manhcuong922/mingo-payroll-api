using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Payroll;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Entities.Evaluation;
using MPS.Domain.Entities.Payroll;
using AttendanceEnums = MPS.Domain.Enums.Attendance;
using ContractEnums = MPS.Domain.Enums.Contract;
using EvaluationEnums = MPS.Domain.Enums.Evaluation;

namespace MPS.Infrastructure.Persistence;

public sealed partial class DatabaseSeeder
{
    /// <summary>
    /// Seed cấu hình lương (bảng công thức mặc định + tham số + hệ số năng suất) và 1 tháng demo
    /// chạy được (chấm công + đánh giá cho NV có HĐ Active) → HR bấm "sinh lương" là tạo được ngay.
    /// </summary>
    public async Task SeedPayrollAsync(CancellationToken ct = default)
    {
        var adminId = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId).Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (adminId == 0)
        {
            _logger.LogWarning("No system admin; skipping payroll seed.");
            return;
        }

        await SeedPayrollFormulaSetAsync(adminId, ct);
        await SeedProductivityCoefficientsAsync(ct);
        await SeedPieceWorkSupportBandsAsync(ct);
        await SeedPayrollDemoPeriodsAsync(adminId, ct);
    }

    private async Task SeedPayrollFormulaSetAsync(int adminId, CancellationToken ct)
    {
        if (await _context.PayrollFormulaSets.AsNoTracking().AnyAsync(ct))
        {
            _logger.LogDebug("PayrollFormulaSet already seeded; checking for missing system columns.");
            await UpgradeDefaultFormulaSetAsync(ct);
            return;
        }

        var set = new PayrollFormulaSet
        {
            Name = DefaultPayrollFormulaSet.Name,
            Description = "Bảng công thức mặc định tái hiện đặc tả sheet 4.LUONG. Kế toán clone rồi chỉnh.",
            IsActive = true,
            IsDefault = true,
            CreatedByUserId = adminId,
            CreatedAt = DateTime.UtcNow,
            Columns = BuildSeedPayrollColumns(),
        };
        await _context.PayrollFormulaSets.AddAsync(set, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded default PayrollFormulaSet with {Count} columns.", set.Columns.Count);
    }

    /// <summary>
    /// Nâng cấp bảng công thức MẶC ĐỊNH khi bản mã nguồn có thêm cột hệ thống mới (BS-5: TRUY_LINH /
    /// TRUY_THU). Chỉ chạm bảng <c>IsDefault</c> và chỉ các cột <c>IsSystem</c> — bảng kế toán tự
    /// clone/chỉnh không bị đụng. Idempotent: chạy lại không đổi gì khi cột đã đủ.
    ///
    /// Cần thiết vì seeder gốc bỏ qua toàn bộ khi đã có bảng ⇒ DB đang chạy sẽ vĩnh viễn thiếu cột
    /// mới, và kỳ lương mới sẽ không có chỗ nhận khoản truy lĩnh.
    /// </summary>
    private async Task UpgradeDefaultFormulaSetAsync(CancellationToken ct)
    {
        var set = await _context.PayrollFormulaSets
            .Include(s => s.Columns)
            .FirstOrDefaultAsync(s => s.IsDefault, ct);
        if (set is null) return;

        var defaults = BuildSeedPayrollColumns();
        var defaultByCode = defaults.ToDictionary(d => d.ColumnCode, StringComparer.OrdinalIgnoreCase);
        var existingCodes = set.Columns.Select(c => c.ColumnCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = defaults.Where(d => !existingCodes.Contains(d.ColumnCode)).ToList();

        foreach (var column in missing) set.Columns.Add(column);

        // Đồng bộ ĐỊNH NGHĨA cột HỆ THỐNG theo bản mã nguồn (nguồn sự thật) — để thay đổi cột hệ thống
        // trong code áp được vào DB đang chạy mà KHÔNG cần migration (kỳ mới dùng snapshot mới; kỳ cũ
        // vẫn giữ snapshot đã đóng băng). VD: #64 nối LookupKey cho cột "Khác", #62 mở editable cột Cờ BH.
        var changed = missing.Count > 0;
        foreach (var column in set.Columns.Where(c => c.IsSystem))
        {
            if (!defaultByCode.TryGetValue(column.ColumnCode, out var d)) continue;
            if (column.DisplayOrder != d.DisplayOrder) { column.DisplayOrder = d.DisplayOrder; changed = true; }
            if (column.FormulaJson != d.FormulaJson) { column.FormulaJson = d.FormulaJson; changed = true; }
            if (column.LookupKey != d.LookupKey) { column.LookupKey = d.LookupKey; changed = true; }
            if (column.ValueSource != d.ValueSource) { column.ValueSource = d.ValueSource; changed = true; }
            if (column.IsEditable != d.IsEditable) { column.IsEditable = d.IsEditable; changed = true; }
            if (column.IsMoney != d.IsMoney) { column.IsMoney = d.IsMoney; changed = true; }
            if (column.DataType != d.DataType) { column.DataType = d.DataType; changed = true; }
            if (column.Rounding != d.Rounding) { column.Rounding = d.Rounding; changed = true; }
            if (column.Label != d.Label) { column.Label = d.Label; changed = true; }
            if (column.GroupName != d.GroupName) { column.GroupName = d.GroupName; changed = true; }
        }

        if (!changed) return;

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Đồng bộ bảng công thức mặc định: {Missing} cột mới ({Codes}) + cập nhật định nghĩa cột hệ thống.",
            missing.Count, string.Join(", ", missing.Select(m => m.ColumnCode)));
    }

    private async Task SeedProductivityCoefficientsAsync(CancellationToken ct)
    {
        if (await _context.ProductivityCoefficients.AsNoTracking().AnyAsync(ct)) return;

        var now = DateTime.UtcNow;
        var rows = new[]
        {
            new ProductivityCoefficient { MinScore = 4.50m, MaxScore = 5.00m, Coefficient = 1.200m, Label = "Xuất sắc", IsActive = true, CreatedAt = now },
            new ProductivityCoefficient { MinScore = 3.50m, MaxScore = 4.49m, Coefficient = 1.000m, Label = "Tốt", IsActive = true, CreatedAt = now },
            new ProductivityCoefficient { MinScore = 2.50m, MaxScore = 3.49m, Coefficient = 0.800m, Label = "Khá", IsActive = true, CreatedAt = now },
            new ProductivityCoefficient { MinScore = 1.00m, MaxScore = 2.49m, Coefficient = 0.500m, Label = "Cần cải thiện", IsActive = true, CreatedAt = now },
        };
        await _context.ProductivityCoefficients.AddRangeAsync(rows, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} ProductivityCoefficient bands.", rows.Length);
    }

    /// <summary>
    /// Phase 2 — bậc hỗ trợ theo tiến độ của HĐ khoán việc (quyết định PO 18/08/2026):
    /// từ 150 giờ = 200.000đ, từ 200 giờ = 500.000đ. KHÔNG cộng dồn — xem
    /// <c>PieceWorkSupportResolver</c>.
    /// </summary>
    private async Task SeedPieceWorkSupportBandsAsync(CancellationToken ct)
    {
        if (await _context.PieceWorkSupportBands.AsNoTracking().AnyAsync(ct)) return;

        var now = DateTime.UtcNow;
        var rows = new[]
        {
            new PieceWorkSupportBand { MinHours = 150m, MaxHours = 200m, Amount = 200_000m, Label = "Từ 150 giờ", IsActive = true, CreatedAt = now },
            new PieceWorkSupportBand { MinHours = 200m, MaxHours = null, Amount = 500_000m, Label = "Từ 200 giờ", IsActive = true, CreatedAt = now },
        };
        await _context.PieceWorkSupportBands.AddRangeAsync(rows, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} PieceWorkSupportBand rows.", rows.Length);
    }

    private const int PayrollDemoMonths = 3;   // 3 kỳ gần nhất (đủ dữ liệu, tránh kỳ hiện tại còn thiếu).

    // Dựng đúng luồng demo nghiệp vụ:
    // - 06/2026: có sẵn kỳ lương nguồn để đối chiếu và làm căn cứ truy lĩnh/truy thu.
    // - 07/2026: chỉ có dữ liệu nguồn đầy đủ; HR tự bấm "Tạo bảng lương" khi demo.
    // - 08/2026: có dữ liệu nguồn liên kết để tạm tính, không tạo sẵn kỳ lương.
    //
    // Idempotent theo (năm, tháng): bật server lại chỉ bổ sung phần còn thiếu, không sinh trùng kỳ.
    private async Task SeedPayrollDemoPeriodsAsync(int adminId, CancellationToken ct)
    {
        if (!await _context.Contracts.AsNoTracking()
                .AnyAsync(c => c.Status == ContractEnums.ContractStatus.Active, ct))
        {
            _logger.LogWarning("Payroll demo: không có HĐ Active nào; bỏ qua sinh kỳ lương demo.");
            return;
        }

        // Phải ưu tiên ca Fulltime 8 giờ. Lấy "bản ghi đầu tiên" theo Id khiến DB mới luôn chọn
        // Ca sáng 4 giờ; hậu quả là phân ca, WFH và chấm công nền mô tả hai chế độ khác nhau.
        var shiftTemplateId = await _context.ShiftTemplates.AsNoTracking()
            .Where(s => s.IsActive && s.Code == "FULLTIME")
            .Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        if (shiftTemplateId is null)
        {
            _logger.LogWarning("Payroll demo: chưa có ShiftTemplate; bỏ qua sinh kỳ lương demo.");
            return;
        }

        // Kỳ nguồn tháng 06: chuẩn bị dữ liệu đầy đủ rồi chỉ sinh nếu chưa tồn tại.
        var june = DemoPeriodStart(1);
        await PreparePayrollMonthDataAsync(june.Year, june.Month, shiftTemplateId.Value, adminId, ct);

        var juneExists = await _context.PayrollPeriods.AsNoTracking()
            .AnyAsync(p => p.PeriodYear == june.Year && p.PeriodMonth == june.Month, ct);
        if (!juneExists)
        {
            try
            {
                await _payrollGenerator.TryGenerateAsync(june.Year, june.Month, adminId, ct);
            }
            catch (Exception ex)
            {
                // Seeder không được làm API chết nếu dữ liệu cấu hình môi trường chưa đủ để sinh kỳ.
                _logger.LogWarning(ex, "Payroll demo: sinh kỳ nguồn {Month:D2}/{Year} thất bại — bỏ qua.",
                    june.Month, june.Year);
            }
        }

        // Khoản điều chỉnh phải được tạo sau kỳ nguồn 06 nhưng trước khi người dùng sinh kỳ 07.
        await SeedPayrollAdjustmentCasesAsync(ct);

        // Tháng 07 là sân khấu demo chính: tuyệt đối không gọi engine sinh kỳ tại đây.
        var july = DemoPeriodStart(2);
        await PreparePayrollMonthDataAsync(july.Year, july.Month, shiftTemplateId.Value, adminId, ct);

        // Tháng 08 chỉ chuẩn bị dữ liệu nguồn để màn tạm tính và bảng phân ca có dữ liệu thật.
        var august = DemoCurrentMonth;
        await PreparePayrollMonthDataAsync(august.Year, august.Month, shiftTemplateId.Value, adminId, ct);

        _logger.LogInformation(
            "Payroll demo setup hoàn tất: kỳ nguồn {June:MM/yyyy}; dữ liệu tạo kỳ {July:MM/yyyy}; dữ liệu tạm tính {August:MM/yyyy}.",
            june.ToDateTime(TimeOnly.MinValue), july.ToDateTime(TimeOnly.MinValue), august.ToDateTime(TimeOnly.MinValue));
    }

    /// <summary>
    /// Điểm đánh giá tháng của dữ liệu demo — RẢI đều trên cả 4 bậc hệ số năng suất
    /// (<see cref="SeedProductivityCoefficientsAsync"/>: 1.2 / 1.0 / 0.8 / 0.5) thay vì gán cứng 4.0
    /// cho mọi người.
    ///
    /// <para>Gán cứng 4.0 khiến hệ số luôn bằng 1.000 ⇒ ba bậc còn lại của bảng hệ số là cấu hình
    /// không bao giờ được dùng, và mọi dòng lương chỉ khác nhau ở mức lương bậc.</para>
    ///
    /// <para>Xác định theo <c>(mã NV, năm, tháng)</c> nên tái lập được y hệt sau mỗi lần dựng lại DB,
    /// mà vẫn đổi giữa các tháng như đánh giá thật.</para>
    ///
    /// <para>⚠️ <b>KHÔNG dùng <c>HashCode.Combine</c> ở đây.</b> .NET random hoá hạt giống của nó theo
    /// từng TIẾN TRÌNH (chống tấn công băm), nên mỗi lần chạy seed ra một bộ điểm khác — đúng phần
    /// mà chú thích trên hứa là "tái lập được". Hậu quả không hiển nhiên: điểm đổi ⇒ bậc hệ số năng
    /// suất đổi (0,5 / 0,8 / 1,0 / 1,2) ⇒ cột "Trợ cấp lương chính" đổi ⇒ tổng thu nhập, thuế TNCN
    /// và thực nhận của cả kỳ đổi theo. Từng làm lệch 10.000.000đ tổng thu nhập của một dòng lương
    /// giữa hai lần dựng DB, khiến việc đối chiếu số trở nên vô nghĩa.</para>
    ///
    /// <para>Thay bằng băm SỐ HỌC thuần: cùng đầu vào ⇒ cùng đầu ra, ở mọi tiến trình, mọi máy.</para>
    /// </summary>
    internal static decimal DemoEvaluationScore(int employeeInformationId, int year, int month) =>
        StableBucket(employeeInformationId, year, month) switch
        {
            0 => 4.70m,   // Xuất sắc  → 1.2
            1 => 4.30m,   // Tốt       → 1.0
            8 => 3.00m,   // Khá       → 0.8
            9 => 2.20m,   // Cần cải thiện → 0.5
            _ => 3.80m,   // Tốt       → 1.0
        };

    /// <summary>
    /// Băm ba số nguyên về một ô 0–9, ổn định tuyệt đối giữa các lần chạy. Ba hằng số là số nguyên
    /// tố lớn quen dùng cho hàm băm không gian — chỉ cần trộn đều, không cần chất lượng mật mã.
    /// </summary>
    private static int StableBucket(int a, int b, int c)
    {
        unchecked
        {
            var mixed = (uint)a * 73_856_093u ^ (uint)b * 19_349_663u ^ (uint)c * 83_492_791u;
            return (int)(mixed % 10u);
        }
    }

    // Chuẩn bị chấm công (đủ ngày công) + đánh giá tháng Approved + hồ sơ chi lương cho MỌI NV trong phạm
    // vi readiness (HĐ non-Cancelled overlap kỳ) → readiness pass để engine sinh được kỳ.
    private async Task PreparePayrollMonthDataAsync(
        int year, int month, int shiftTemplateId, int adminId, CancellationToken ct)
    {
        var periodStart = new DateOnly(year, month, 1);
        var periodEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var startDt = periodStart.ToDateTime(TimeOnly.MinValue);
        var endDt = periodEnd.ToDateTime(TimeOnly.MaxValue);

        var empIds = await _context.Contracts.AsNoTracking()
            .Where(c => c.Status != ContractEnums.ContractStatus.Cancelled
                && c.EffectiveFrom <= endDt && (c.EffectiveTo == null || c.EffectiveTo >= startDt))
            .Select(c => c.EmployeeInformationId).Distinct().ToListAsync(ct);
        if (empIds.Count == 0) return;

        var emps = await _context.EmployeeInformations
            .Include(e => e.User)
            .Where(e => empIds.Contains(e.Id)).ToListAsync(ct);

        // Ngày nghỉ lễ KHÔNG sinh bản ghi ca chính: đó là ngày nghỉ hưởng nguyên lương, chấm công
        // "đi làm 8 giờ" vào đó là mô tả sai và còn đẻ ra phụ trội loại ngày không có thật.
        var holidays = await LoadHolidayDatesAsync(ct);
        var workdays = new List<DateOnly>();
        for (var d = 1; d <= DateTime.DaysInMonth(year, month); d++)
        {
            var day = new DateOnly(year, month, d);
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (holidays.Contains(day)) continue;
            workdays.Add(day);
        }

        // Bảng phân ca là nguồn dữ liệu thật của chấm công/WFH. Seeder cũ chỉ ghi AttendanceRecord
        // nên màn phân ca tháng 07/08 trắng dù bảng lương vẫn có công.
        await EnsurePayrollMonthScheduleAsync(
            periodStart, periodEnd, shiftTemplateId, adminId, emps.Select(e => e.Id).ToList(), workdays, ct);

        // Đơn WFH phải được áp SAU khi đã có phân ca và TRƯỚC khi điền attendance nền, đúng thứ tự
        // của repository sản phẩm: chỉ ngày có assignment mới được tự sinh công WFH.
        if (periodStart == DemoPeriodStart(2))
            await SeedJulyWfhPayrollCaseAsync(adminId, ct);
        if (periodStart == DemoCurrentMonth)
            await SeedWfhDemoCasesAsync(adminId, ct);

        var assignments = await _context.EmployeeShiftAssignments.AsNoTracking()
            .Where(a => empIds.Contains(a.EmployeeInformationId)
                && a.WorkDate >= periodStart && a.WorkDate <= periodEnd)
            .Select(a => new { a.EmployeeInformationId, a.WorkDate, a.ShiftTemplateId })
            .ToListAsync(ct);
        var assignmentsByEmployee = assignments
            .GroupBy(a => a.EmployeeInformationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var existingAttendanceKeys = (await _context.AttendanceRecords.AsNoTracking()
            .Where(a => empIds.Contains(a.EmployeeInformationId)
                && a.WorkDate >= periodStart && a.WorkDate <= periodEnd
                && a.ShiftTemplateId != null)
            .Select(a => new { a.EmployeeInformationId, a.WorkDate, ShiftTemplateId = a.ShiftTemplateId!.Value })
            .ToListAsync(ct))
            .Select(a => (a.EmployeeInformationId, a.WorkDate, a.ShiftTemplateId))
            .ToHashSet();

        var rubric = await LoadRubricTemplateAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var emp in emps)
        {
            // Số tài khoản / mã số thuế còn trống → điền giá trị số hợp lệ (không dùng chuỗi "DEMO...").
            if (string.IsNullOrWhiteSpace(emp.BankNumber)) emp.BankNumber = $"{190_300_000_000L + emp.Id}";
            if (string.IsNullOrWhiteSpace(emp.TaxNo)) emp.TaxNo = $"{8_000_000_000L + emp.Id}";

            // Bổ sung THEO TỪNG CA còn thiếu. Guard cũ theo cả tháng làm một đơn WFH hai ngày khiến
            // 21 ngày còn lại không có chấm công; đồng thời dữ liệu case một ngày cũng chặn toàn bộ nền.
            if (assignmentsByEmployee.TryGetValue(emp.Id, out var employeeAssignments))
            {
                // Đơn nghỉ dài ngày có thể kéo sang tháng 08 (điển hình thai sản). Nếu chỉ lớp case
                // tháng 07 biết đơn nghỉ còn bước prepare tháng 08 luôn điền Present, cùng một đơn sẽ
                // cho hai trạng thái trái nhau giữa hai tháng. Mỗi ca nền phải đối chiếu đơn Approved.
                var approvedLeaveDays = await LoadApprovedLeaveDaysAsync(emp.Id, periodStart, ct);
                foreach (var assignment in employeeAssignments)
                {
                    if (existingAttendanceKeys.Contains((emp.Id, assignment.WorkDate, assignment.ShiftTemplateId)))
                        continue;

                    var onLeave = approvedLeaveDays.Contains(assignment.WorkDate);

                    await _context.AttendanceRecords.AddAsync(new AttendanceRecord
                    {
                        EmployeeInformationId = emp.Id,
                        WorkDate = assignment.WorkDate,
                        ShiftTemplateId = assignment.ShiftTemplateId,
                        DayHours = onLeave ? 0m : 8m,
                        NightHours = 0m,
                        Status = onLeave
                            ? AttendanceEnums.AttendanceStatus.OnLeave
                            : AttendanceEnums.AttendanceStatus.Present,
                        // Ngày quá khứ phải có đủ giờ vào/ra — thiếu CheckOutAt khiến bản ghi trông như
                        // nhân viên "đang làm việc" dù ngày đã qua từ lâu.
                        CheckInAt = onLeave ? null : assignment.WorkDate.ToDateTime(new TimeOnly(8, 0)),
                        CheckOutAt = onLeave ? null : assignment.WorkDate.ToDateTime(new TimeOnly(17, 0)),
                        Notes = onLeave ? "Nghỉ theo đơn đã duyệt" : null,
                        CreatedAt = now,
                        CreatedByUserId = adminId,
                    }, ct);
                }
            }

            if (emp.UserId is int uid)
            {
                // Unique (user, năm, tháng) — nếu đã có eval (vd từ eval demo) thì CHỈ nâng lên Approved,
                // KHÔNG chèn dòng thứ 2 (tránh vi phạm unique index D11).
                var existingEval = await _context.MonthlyEvaluations
                    .FirstOrDefaultAsync(m => m.EmployeeUserId == uid && m.PeriodYear == year && m.PeriodMonth == month, ct);
                if (existingEval is null)
                {
                    await _context.MonthlyEvaluations.AddAsync(new MonthlyEvaluation
                    {
                        EmployeeUserId = uid,
                        PeriodYear = year,
                        PeriodMonth = month,
                        SubjectRoleIdSnapshot = emp.User?.RoleId ?? SystemRoles.EmployeeId,
                        Source = EvaluationEnums.EvaluationSource.Direct,
                        FinalScore = DemoEvaluationScore(emp.Id, year, month),
                        Status = EvaluationEnums.EvaluationStatus.Approved,
                        ApprovedByUserId = adminId,
                        ApprovedAt = now,
                        CreatedByUserId = adminId,
                        CreatedAt = now,
                        Scores = rubric.Count > 0 ? ScoredLines(rubric, uid + month) : new List<EvaluationScoreLine>(),
                    }, ct);
                }
                else if (existingEval.Status != EvaluationEnums.EvaluationStatus.Approved)
                {
                    existingEval.Status = EvaluationEnums.EvaluationStatus.Approved;
                    existingEval.ApprovedByUserId = adminId;
                    existingEval.ApprovedAt = now;
                }
            }
        }

        await _context.SaveChangesAsync(ct);
    }
}
