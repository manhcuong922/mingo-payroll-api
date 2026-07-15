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
        await SeedPayrollParametersAsync(ct);
        await SeedProductivityCoefficientsAsync(ct);
        await SeedPayrollDemoMonthAsync(adminId, ct);
    }

    private async Task SeedPayrollFormulaSetAsync(int adminId, CancellationToken ct)
    {
        if (await _context.PayrollFormulaSets.AsNoTracking().AnyAsync(ct))
        {
            _logger.LogDebug("PayrollFormulaSet already seeded; skipping.");
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

    private async Task SeedPayrollParametersAsync(CancellationToken ct)
    {
        if (await _context.PayrollParameters.AsNoTracking().AnyAsync(p => p.Key == PayrollParameterKeys.MealPricePerWorkday, ct))
            return;

        await _context.PayrollParameters.AddAsync(new PayrollParameter
        {
            Key = PayrollParameterKeys.MealPricePerWorkday,
            Name = "Đơn giá cơm mỗi công",
            Value = 40_000m,
            Description = "Tiền cơm 1 công (đặc tả V9). Chính sách công ty — không phải luật.",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        }, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded PayrollParameter MEAL_PRICE_PER_WORKDAY = 40,000.");
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

    // Best-effort: chuẩn bị đủ liệu 1 tháng cho NV có HĐ Active → readiness pass, bấm sinh lương chạy ngay.
    private async Task SeedPayrollDemoMonthAsync(int adminId, CancellationToken ct)
    {
        if (await _context.PayrollPeriods.AsNoTracking().AnyAsync(ct)) return;

        var now = DateTime.UtcNow;
        var demo = new DateOnly(now.Year, now.Month, 1).AddMonths(-1); // tháng trước (đủ dữ liệu)
        int year = demo.Year, month = demo.Month;
        var periodStart = new DateOnly(year, month, 1);
        var periodEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var startDt = periodStart.ToDateTime(TimeOnly.MinValue);
        var endDt = periodEnd.ToDateTime(TimeOnly.MaxValue);

        var contracts = await _context.Contracts
            .Include(c => c.EmployeeInformation).ThenInclude(e => e.User)
            .Where(c => c.Status == ContractEnums.ContractStatus.Active
                && c.EffectiveFrom <= endDt && (c.EffectiveTo == null || c.EffectiveTo >= startDt))
            .Take(6).ToListAsync(ct);
        if (contracts.Count == 0)
        {
            _logger.LogWarning("Payroll demo: no active contract overlaps {Month:D2}/{Year}; skipping demo month.", month, year);
            return;
        }

        var shiftTemplateId = await _context.ShiftTemplates.AsNoTracking()
            .Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        if (shiftTemplateId is null)
        {
            _logger.LogWarning("Payroll demo: no ShiftTemplate; skipping demo month.");
            return;
        }

        var workdays = new List<DateOnly>();
        for (var d = 1; d <= DateTime.DaysInMonth(year, month); d++)
        {
            var day = new DateOnly(year, month, d);
            if (day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) workdays.Add(day);
        }

        var seeded = 0;
        foreach (var c in contracts)
        {
            var emp = c.EmployeeInformation;
            if (emp is null) continue;

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
                var hasEval = await _context.MonthlyEvaluations
                    .AnyAsync(m => m.EmployeeUserId == uid && m.PeriodYear == year && m.PeriodMonth == month, ct);
                if (!hasEval)
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
            }
            seeded++;
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Payroll demo month {Month:D2}/{Year}: chuẩn bị chấm công + đánh giá cho {Count} NV (HĐ Active). " +
            "HR có thể bấm sinh lương kỳ này.", month, year, seeded);
    }
}
