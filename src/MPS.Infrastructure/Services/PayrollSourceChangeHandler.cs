using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Payroll;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Services;

/// <summary>
/// Điểm gộp BS-6 + BS-5: nhận thông báo "nguồn của tháng X đổi cho NV Y" và tự phân nhánh:
///   - Kỳ Draft ⇒ <see cref="IPayrollStaleMarker"/> đánh cờ.
///   - Kỳ Finalized ⇒ <see cref="IPayrollAdjustmentCapture"/> capture chênh lệch NV.
/// Handler nguồn chỉ cần gọi 1 dòng thay vì nhớ 2 nhánh, giảm nguy cơ quên nối một nhánh khi thêm
/// handler mới.
/// </summary>
public sealed class PayrollSourceChangeHandler : IPayrollSourceChangeHandler
{
    private readonly AppDbContext _db;
    private readonly IPayrollStaleMarker _stale;
    private readonly IPayrollAdjustmentCapture _capture;
    private readonly ILogger<PayrollSourceChangeHandler> _logger;

    public PayrollSourceChangeHandler(
        AppDbContext db,
        IPayrollStaleMarker stale,
        IPayrollAdjustmentCapture capture,
        ILogger<PayrollSourceChangeHandler> logger)
    {
        _db = db;
        _stale = stale;
        _capture = capture;
        _logger = logger;
    }

    public async Task OnSourceChangedAsync(
        IEnumerable<(int Year, int Month)> months,
        IReadOnlyCollection<int> employeeInformationIds,
        string reason,
        CancellationToken ct = default)
    {
        var distinctMonths = months.Distinct().ToList();
        if (distinctMonths.Count == 0) return;

        try
        {
            await _stale.MarkStaleForMonthsAsync(distinctMonths, reason, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mark stale thất bại ({Reason}).", reason);
        }

        if (employeeInformationIds.Count == 0) return;

        var finalizedIds = await FindFinalizedPeriodIdsAsync(distinctMonths, ct);
        foreach (var periodId in finalizedIds)
            await _capture.CaptureForFinalizedPeriodAsync(periodId, employeeInformationIds, reason, ct);
    }

    public Task OnSourceChangedAsync(
        int year, int month, int employeeInformationId, string reason, CancellationToken ct = default) =>
        OnSourceChangedAsync(new[] { (year, month) }, new[] { employeeInformationId }, reason, ct);

    public async Task OnEffectiveFromChangedAsync(
        int year, int month, int employeeInformationId, string reason, CancellationToken ct = default)
    {
        try
        {
            await _stale.MarkStaleFromMonthAsync(year, month, reason, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mark stale từ tháng thất bại ({Reason}).", reason);
        }

        // Kỳ Finalized nằm sau tháng hiệu lực (VD: ký phụ lục sau khi kỳ đó đã chốt) → capture để trả bù.
        var floor = year * 12 + month;
        List<int> finalizedIds;
        try
        {
            finalizedIds = await _db.PayrollPeriods
                .Where(p => p.Status == PayrollPeriodStatus.Finalized
                            && p.PeriodYear * 12 + p.PeriodMonth >= floor)
                .Select(p => p.Id)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tìm kỳ Finalized từ tháng thất bại — bỏ qua nhánh capture adjustment.");
            return;
        }

        var empIds = new[] { employeeInformationId };
        foreach (var periodId in finalizedIds)
            await _capture.CaptureForFinalizedPeriodAsync(periodId, empIds, reason, ct);
    }

    private async Task<List<int>> FindFinalizedPeriodIdsAsync(
        IReadOnlyList<(int Year, int Month)> months, CancellationToken ct)
    {
        try
        {
            var result = new List<int>(months.Count);
            foreach (var (year, month) in months)
            {
                var id = await _db.PayrollPeriods
                    .Where(p => p.PeriodYear == year && p.PeriodMonth == month
                                && p.Status == PayrollPeriodStatus.Finalized)
                    .Select(p => p.Id)
                    .FirstOrDefaultAsync(ct);
                if (id > 0) result.Add(id);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tìm kỳ Finalized thất bại — bỏ qua nhánh capture adjustment.");
            return new List<int>();
        }
    }
}
