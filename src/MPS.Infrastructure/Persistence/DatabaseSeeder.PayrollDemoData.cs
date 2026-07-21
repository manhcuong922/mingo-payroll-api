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
            Columns = DefaultPayrollFormulaSet.BuildColumns(),
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

        var defaults = DefaultPayrollFormulaSet.BuildColumns();
        var existingCodes = set.Columns.Select(c => c.ColumnCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = defaults.Where(d => !existingCodes.Contains(d.ColumnCode)).ToList();
        if (missing.Count == 0) return;

        foreach (var column in missing) set.Columns.Add(column);

        // Cột mới nằm giữa bảng ⇒ đồng bộ lại DisplayOrder + công thức của các cột hệ thống theo
        // bản mặc định (THUCNHAN phải cộng thêm 2 cột mới, nếu không tiền truy lĩnh sẽ không chảy vào).
        var defaultByCode = defaults.ToDictionary(d => d.ColumnCode, StringComparer.OrdinalIgnoreCase);
        foreach (var column in set.Columns.Where(c => c.IsSystem))
        {
            if (!defaultByCode.TryGetValue(column.ColumnCode, out var d)) continue;
            column.DisplayOrder = d.DisplayOrder;
            column.FormulaJson = d.FormulaJson;
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Nâng cấp bảng công thức mặc định: thêm {Count} cột hệ thống mới ({Codes}).",
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

    private const int PayrollDemoMonths = 3;   // 3 kỳ gần nhất (đủ dữ liệu, tránh kỳ hiện tại còn thiếu).

    // Chuẩn bị đủ liệu + SINH nhiều kỳ lương demo hoàn chỉnh (readiness → engine thật) cho công ty có
    // nhiều HĐ Active nhất → màn bảng lương có sẵn số liệu, không cần HR bấm. Best-effort từng kỳ.
    private async Task SeedPayrollDemoPeriodsAsync(int adminId, CancellationToken ct)
    {
        if (await _context.PayrollPeriods.AsNoTracking().AnyAsync(ct)) return;

        if (!await _context.Contracts.AsNoTracking()
                .AnyAsync(c => c.Status == ContractEnums.ContractStatus.Active, ct))
        {
            _logger.LogWarning("Payroll demo: không có HĐ Active nào; bỏ qua sinh kỳ lương demo.");
            return;
        }

        var shiftTemplateId = await _context.ShiftTemplates.AsNoTracking()
            .Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        if (shiftTemplateId is null)
        {
            _logger.LogWarning("Payroll demo: chưa có ShiftTemplate; bỏ qua sinh kỳ lương demo.");
            return;
        }

        var now = DateTime.UtcNow;
        var generated = 0;
        // Sinh từ CŨ → MỚI để law-change note kỳ sau so đúng kỳ trước.
        for (var back = PayrollDemoMonths; back >= 1; back--)
        {
            var anchor = new DateOnly(now.Year, now.Month, 1).AddMonths(-back);
            int year = anchor.Year, month = anchor.Month;

            await PreparePayrollMonthDataAsync(year, month, shiftTemplateId.Value, adminId, ct);
            try
            {
                var id = await _payrollGenerator.TryGenerateAsync(year, month, adminId, ct);
                if (id is not null) generated++;
            }
            catch (Exception ex)
            {
                // Demo: KHÔNG để lỗi sinh kỳ chặn khởi động app (vd thiếu version compliance ở tháng đó).
                _logger.LogWarning(ex, "Payroll demo: sinh kỳ {Month:D2}/{Year} thất bại — bỏ qua.", month, year);
            }
        }

        _logger.LogInformation("Payroll demo: đã sinh {Count}/{Total} kỳ lương.", generated, PayrollDemoMonths);
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

        var workdays = new List<DateOnly>();
        for (var d = 1; d <= DateTime.DaysInMonth(year, month); d++)
        {
            var day = new DateOnly(year, month, d);
            if (day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) workdays.Add(day);
        }

        var now = DateTime.UtcNow;
        foreach (var emp in emps)
        {
            if (string.IsNullOrWhiteSpace(emp.BankNumber)) emp.BankNumber = $"DEMO{emp.Id:D10}";
            if (string.IsNullOrWhiteSpace(emp.TaxNo)) emp.TaxNo = $"{emp.Id:D10}";

            var hasAtt = await _context.AttendanceRecords
                .AnyAsync(a => a.EmployeeInformationId == emp.Id && a.WorkDate >= periodStart && a.WorkDate <= periodEnd, ct);
            if (!hasAtt)
            {
                foreach (var day in workdays)
                {
                    await _context.AttendanceRecords.AddAsync(new AttendanceRecord
                    {
                        EmployeeInformationId = emp.Id,
                        WorkDate = day,
                        ShiftTemplateId = shiftTemplateId,
                        DayHours = 8m,
                        NightHours = 0m,
                        Status = AttendanceEnums.AttendanceStatus.Present,
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
                        FinalScore = 4.0m,
                        Status = EvaluationEnums.EvaluationStatus.Approved,
                        ApprovedByUserId = adminId,
                        ApprovedAt = now,
                        CreatedByUserId = adminId,
                        CreatedAt = now,
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
