using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Enums.Evaluation;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Services;

/// <inheritdoc />
public sealed class PayrollReadinessChecker : IPayrollReadinessChecker
{
    private readonly AppDbContext _db;
    public PayrollReadinessChecker(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PayrollEmployeeReadiness>> CheckAsync(PayrollReadinessRequest req, CancellationToken ct)
    {
        var periodStart = new DateOnly(req.Year, req.Month, 1);
        var periodEnd = new DateOnly(req.Year, req.Month, DateTime.DaysInMonth(req.Year, req.Month));
        var startDt = periodStart.ToDateTime(TimeOnly.MinValue);
        var endDt = periodEnd.ToDateTime(TimeOnly.MaxValue);

        var contracts = await _db.Contracts.AsNoTracking()
            .Where(c => c.Status != ContractStatus.Cancelled
                && c.EffectiveFrom <= endDt
                && (c.EffectiveTo == null || c.EffectiveTo >= startDt))
            .Select(c => new { c.EmployeeInformationId, c.Status })
            .ToListAsync(ct);

        var empIds = contracts.Select(c => c.EmployeeInformationId).Distinct().ToList();
        if (empIds.Count == 0) return Array.Empty<PayrollEmployeeReadiness>();

        var activeContractEmp = contracts.Where(c => c.Status == ContractStatus.Active)
            .Select(c => c.EmployeeInformationId).ToHashSet();

        var employees = await _db.EmployeeInformations.AsNoTracking()
            .Where(e => empIds.Contains(e.Id))
            .Select(e => new
            {
                e.Id,
                e.UserId,
                e.FullName,
                e.BankNumber,
                e.TaxNo,
                Code = e.User != null ? e.User.EmployeeCode : e.CitizenId,
            })
            .ToListAsync(ct);

        var hasAttendance = (await _db.AttendanceRecords.AsNoTracking()
            .Where(a => empIds.Contains(a.EmployeeInformationId) && a.WorkDate >= periodStart && a.WorkDate <= periodEnd)
            .Select(a => a.EmployeeInformationId).Distinct().ToListAsync(ct)).ToHashSet();

        var hasPendingOt = (await _db.OvertimeRequests.AsNoTracking()
            .Where(o => empIds.Contains(o.EmployeeInformationId) && o.WorkDate >= periodStart && o.WorkDate <= periodEnd
                && (o.Status == OtRequestStatus.Pending || o.Status == OtRequestStatus.PendingApprover))
            .Select(o => o.EmployeeInformationId).Distinct().ToListAsync(ct)).ToHashSet();

        var userIds = employees.Where(e => e.UserId.HasValue).Select(e => e.UserId!.Value).ToList();
        var hasApprovedEval = (await _db.MonthlyEvaluations.AsNoTracking()
            .Where(m => userIds.Contains(m.EmployeeUserId) && m.PeriodYear == req.Year && m.PeriodMonth == req.Month
                && m.Status == EvaluationStatus.Approved)
            .Select(m => m.EmployeeUserId).Distinct().ToListAsync(ct)).ToHashSet();

        var result = new List<PayrollEmployeeReadiness>();
        foreach (var e in employees.OrderBy(e => e.Code))
        {
            var missing = new List<string>();
            if (!activeContractEmp.Contains(e.Id)) missing.Add("Chưa có hợp đồng hiệu lực (Active) trong kỳ");
            if (!hasAttendance.Contains(e.Id)) missing.Add("Chưa có dữ liệu chấm công trong kỳ");
            if (hasPendingOt.Contains(e.Id)) missing.Add("Còn đơn tăng ca chưa duyệt xong");
            if (!e.UserId.HasValue || !hasApprovedEval.Contains(e.UserId.Value)) missing.Add("Chưa có bảng đánh giá tháng được duyệt");
            if (string.IsNullOrWhiteSpace(e.BankNumber) || string.IsNullOrWhiteSpace(e.TaxNo)) missing.Add("Hồ sơ chi lương thiếu (số TK / mã số thuế)");
            result.Add(new PayrollEmployeeReadiness(e.Id, e.Code ?? string.Empty, e.FullName, missing));
        }
        return result;
    }
}
