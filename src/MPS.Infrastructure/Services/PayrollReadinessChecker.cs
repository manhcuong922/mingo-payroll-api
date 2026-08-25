using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Domain.Constants;
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
            .Select(c => new { c.EmployeeInformationId, c.Status, c.Kind })
            .ToListAsync(ct);

        var empIds = contracts.Select(c => c.EmployeeInformationId).Distinct().ToList();
        if (empIds.Count == 0) return Array.Empty<PayrollEmployeeReadiness>();

        // #60: đủ điều kiện HĐ = có HĐ ĐÃ KÝ (bất kỳ trạng thái != Draft) phủ khoảng hiệu lực lên kỳ —
        // KHÔNG hỏi "HĐ còn Active BÂY GIỜ không". Người chấm dứt/hết hạn giữa tháng vẫn phải chốt lương
        // những ngày đã làm; chỉ HĐ còn Draft (chưa ai ký) mới tính là thiếu dữ liệu.
        var signedContractEmp = contracts.Where(c => c.Status != ContractStatus.Draft)
            .Select(c => c.EmployeeInformationId).ToHashSet();

        // ⭐ Phase 3 — NV chỉ có hợp đồng DÂN SỰ (khoán việc / dịch vụ / CTV) KHÔNG bị đòi bảng đánh
        // giá tháng: đánh giá là công cụ quản trị của quan hệ LAO ĐỘNG, người ký HĐ dân sự không nằm
        // trong quy trình đó nên sẽ không bao giờ có bản được duyệt. Mà `CreatePayrollPeriodCommand`
        // chặn CẢ KỲ khi bất kỳ NV nào chưa đủ liệu ⇒ chỉ một hợp đồng khoán là không tạo được bảng
        // lương cho toàn công ty.
        //
        // Chấm công thì VẪN đòi (quyết định PO 18/08/2026): mốc giờ của khoản hỗ trợ theo tiến độ
        // trên HĐ khoán lấy từ chính bảng chấm công / phân ca.
        //
        // "Chỉ có HĐ dân sự" chứ không phải "có ít nhất một HĐ dân sự": NV vừa có HĐLĐ vừa có HĐ
        // dịch vụ trong kỳ vẫn phải có đánh giá cho phần HĐLĐ.
        var civilOnlyEmp = contracts
            .GroupBy(c => c.EmployeeInformationId)
            .Where(g => g.All(c => ContractKindMetadata.Get(c.Kind).LegalFramework == ContractLegalFramework.CivilCode))
            .Select(g => g.Key)
            .ToHashSet();

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

        var warnings = await BuildPeriodWarningsAsync(req, periodStart, periodEnd, ct);

        var result = new List<PayrollEmployeeReadiness>();
        foreach (var e in employees.OrderBy(e => e.Code))
        {
            var missing = new List<string>();
            if (!signedContractEmp.Contains(e.Id)) missing.Add(PayrollReadinessBlockers.NoSignedContract);
            if (!hasAttendance.Contains(e.Id)) missing.Add(PayrollReadinessBlockers.NoAttendance);
            if (hasPendingOt.Contains(e.Id)) missing.Add(PayrollReadinessBlockers.PendingOvertime);
            if (!civilOnlyEmp.Contains(e.Id)
                && (!e.UserId.HasValue || !hasApprovedEval.Contains(e.UserId.Value)))
                missing.Add(PayrollReadinessBlockers.NoApprovedEvaluation);
            if (string.IsNullOrWhiteSpace(e.BankNumber) || string.IsNullOrWhiteSpace(e.TaxNo)) missing.Add(PayrollReadinessBlockers.IncompletePaymentProfile);
            result.Add(new PayrollEmployeeReadiness(e.Id, e.Code ?? string.Empty, e.FullName, missing) { Warnings = warnings });
        }
        return result;
    }

    /// <summary>
    /// #59/HOL-WARN01 ([D17]) — cảnh báo cấp kỳ, KHÔNG chặn: tháng có ngày lễ dương lịch cố định theo
    /// luật nhưng chưa nhập mốc đó vào bảng <c>PublicHolidays</c> ⇒ công chuẩn và phụ trội ngày lễ có
    /// thể sai. Trả rỗng nếu tháng không có lễ luật cố định hoặc đã nhập đủ.
    /// </summary>
    private async Task<IReadOnlyList<string>> BuildPeriodWarningsAsync(
        PayrollReadinessRequest req, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct)
    {
        var statutory = LaborLawConstants.PublicHolidays.FixedSolarInMonth(req.Year, req.Month).ToList();
        if (statutory.Count == 0) return Array.Empty<string>();

        var entered = await _db.PublicHolidays.AsNoTracking()
            .Where(h => h.IsActive && h.HolidayDate <= periodEnd && h.HolidayEndDate >= periodStart)
            .Select(h => new { h.HolidayDate, h.HolidayEndDate })
            .ToListAsync(ct);

        bool IsCovered(DateOnly d) => entered.Any(h => h.HolidayDate <= d && h.HolidayEndDate >= d);
        var missingStatutory = statutory.Where(d => !IsCovered(d)).ToList();
        if (missingStatutory.Count == 0) return Array.Empty<string>();

        return new[]
        {
            $"Tháng {req.Month:D2}/{req.Year} có ngày lễ theo luật ({string.Join(", ", missingStatutory.Select(d => d.ToString("dd/MM")))}) "
            + "nhưng chưa nhập vào bảng ngày lễ (PublicHolidays) — công chuẩn và phụ trội ngày lễ có thể bị sai. "
            + "Hãy nhập ngày lễ trước khi chốt lương.",
        };
    }
}
