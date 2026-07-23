using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Payroll;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Services;

/// <summary>
/// BS-5 — capture khoản chênh lệch THUCNHAN của NV khi kỳ Finalized bị sửa nguồn.
///
/// Cách tính chênh lệch:
///   - Đọc THUCNHAN cũ của dòng NV trong kỳ (giá trị hiệu lực: Override nếu có, ngược lại Computed).
///   - Collect nguồn MỚI cho đúng NV đó tại payDate của kỳ.
///   - Build lại plan từ <c>FormulaSnapshotJson</c> (công thức đã đóng băng) — không dùng công thức
///     hiện tại; nếu bảng gốc đã sửa, capture vẫn dùng đúng công thức lúc chốt kỳ.
///   - ComputeAll dòng tạm + áp lại các ô ghim cũ → THUCNHAN mới.
///   - Diff = new − old. 0 → xoá adjustment PENDING (nếu có, coi như trở lại đúng); khác 0 → upsert.
///
/// Best-effort: mọi lỗi được nuốt + log để không làm hỏng thao tác nguồn.
/// </summary>
public sealed class PayrollAdjustmentCapture : IPayrollAdjustmentCapture
{
    private const int ReasonMaxLength = 500;

    private readonly AppDbContext _db;
    private readonly IPayrollDataCollector _collector;
    private readonly IPayrollCalculator _calculator;
    private readonly IColumnDependencyGraph _graph;
    private readonly IPayrollFieldCatalog _catalog;
    private readonly Application.Common.Interfaces.ICurrentUserService _currentUser;
    private readonly ILogger<PayrollAdjustmentCapture> _logger;

    public PayrollAdjustmentCapture(
        AppDbContext db,
        IPayrollDataCollector collector,
        IPayrollCalculator calculator,
        IColumnDependencyGraph graph,
        IPayrollFieldCatalog catalog,
        Application.Common.Interfaces.ICurrentUserService currentUser,
        ILogger<PayrollAdjustmentCapture> logger)
    {
        _db = db;
        _collector = collector;
        _calculator = calculator;
        _graph = graph;
        _catalog = catalog;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task CaptureForFinalizedPeriodAsync(
        int sourcePeriodId,
        IReadOnlyCollection<int> employeeInformationIds,
        string reason,
        CancellationToken ct = default)
    {
        if (employeeInformationIds.Count == 0) return;

        try
        {
            // `AsSplitQuery` là BẮT BUỘC ở đây, không phải tối ưu cho vui: kỳ lương có ~34 dòng ×
            // ~54 ô, nạp bằng Include lồng nhau sinh tích Descartes và truy vấn **quá hạn SQL
            // (Execution Timeout)**. Vì cả khối này là best-effort (nuốt lỗi để không làm hỏng thao
            // tác nguồn), timeout đó khiến khoản truy lĩnh/truy thu **âm thầm không bao giờ được
            // sinh** — người lao động mất tiền truy lĩnh mà không ai thấy lỗi ở đâu.
            var period = await _db.PayrollPeriods
                .Include(p => p.Rows).ThenInclude(r => r.Cells)
                .AsSplitQuery()
                .FirstOrDefaultAsync(p => p.Id == sourcePeriodId, ct);

            if (period is null || period.Status != PayrollPeriodStatus.Finalized) return;

            var defs = PayrollColumnSnapshotMapper.Deserialize(period.FormulaSnapshotJson);
            var plan = PayrollColumnPlan.Build(defs, _graph);

            var empRows = period.Rows
                .Where(r => r.RowType == PayrollRowType.Employee && r.EmployeeInformationId.HasValue)
                .ToDictionary(r => r.EmployeeInformationId!.Value);

            var relevantIds = employeeInformationIds.Where(empRows.ContainsKey).ToList();
            if (relevantIds.Count == 0) return;

            var collection = await _collector.CollectAsync(
                new PayrollCollectionRequest(
                    period.PeriodYear, period.PeriodMonth, period.PayDate,
                    period.StandardWorkingDays, relevantIds), ct);

            var inputsByEmp = collection.Employees.ToDictionary(e => e.EmployeeInformationId);
            var trimmedReason = reason.Length <= ReasonMaxLength ? reason : reason[..ReasonMaxLength];
            var nowUtc = DateTime.UtcNow;
            var userId = _currentUser.UserId ?? period.CreatedByUserId;

            foreach (var empId in relevantIds)
            {
                if (!inputsByEmp.TryGetValue(empId, out var newInputs)) continue;
                var oldRow = empRows[empId];

                var oldThucNhan = EffectiveValue(oldRow, PayrollColumnCodes.ThucNhan) ?? 0m;
                var newThucNhan = ComputeThucNhanWithFrozenPlan(plan, collection, newInputs, oldRow);

                var diff = decimal.Round(newThucNhan - oldThucNhan, 2, MidpointRounding.AwayFromZero);
                await UpsertPendingAsync(sourcePeriodId, empId, diff, trimmedReason, userId, nowUtc, ct);
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "BS-5 capture thất bại cho kỳ #{PeriodId}, {Count} NV. Lý do: {Reason}",
                sourcePeriodId, employeeInformationIds.Count, reason);
        }
    }

    private decimal ComputeThucNhanWithFrozenPlan(
        PayrollColumnPlan plan,
        PayrollCollectionResult collection,
        PayrollEmployeeInputs newInputs,
        PayrollRow oldRow)
    {
        var tempRow = new PayrollRow
        {
            RowType = PayrollRowType.Employee,
            EmployeeInformationId = newInputs.EmployeeInformationId,
        };

        var ctx = new PayrollCalcContext
        {
            Fields = newInputs.ToFields(_catalog),
            FunctionContext = collection.FunctionContext,
        };

        _calculator.ComputeAll(plan, ctx, tempRow);

        foreach (var oldCell in oldRow.Cells.Where(c => c.IsOverridden && c.OverrideValue.HasValue))
        {
            if (!plan.ByCode.TryGetValue(oldCell.ColumnCode, out var col) || !col.IsEditable) continue;

            var newCell = tempRow.Cells.FirstOrDefault(c => c.ColumnCode == oldCell.ColumnCode);
            if (newCell is null)
            {
                newCell = new PayrollCellValue { ColumnCode = oldCell.ColumnCode };
                tempRow.Cells.Add(newCell);
            }
            newCell.IsOverridden = true;
            newCell.OverrideValue = oldCell.OverrideValue;

            _calculator.Recompute(plan, ctx, tempRow, oldCell.ColumnCode);
        }

        return EffectiveValue(tempRow, PayrollColumnCodes.ThucNhan) ?? 0m;
    }

    private static decimal? EffectiveValue(PayrollRow row, string columnCode)
    {
        var cell = row.Cells.FirstOrDefault(c => c.ColumnCode == columnCode);
        if (cell is null) return null;
        return cell.IsOverridden ? cell.OverrideValue : cell.ComputedValue;
    }

    private async Task UpsertPendingAsync(
        int sourcePeriodId, int empId, decimal amount, string reason,
        int userId, DateTime nowUtc, CancellationToken ct)
    {
        var existing = await _db.PayrollAdjustments
            .FirstOrDefaultAsync(a =>
                a.SourcePeriodId == sourcePeriodId
                && a.EmployeeInformationId == empId
                && a.AppliedPeriodId == null, ct);

        if (amount == 0m)
        {
            if (existing is not null) _db.PayrollAdjustments.Remove(existing);
            return;
        }

        if (existing is null)
        {
            _db.PayrollAdjustments.Add(new PayrollAdjustment
            {
                SourcePeriodId = sourcePeriodId,
                EmployeeInformationId = empId,
                Amount = amount,
                Reason = reason,
                CreatedAt = nowUtc,
                CreatedByUserId = userId,
            });
        }
        else
        {
            existing.Amount = amount;
            existing.Reason = reason;
            existing.CreatedAt = nowUtc;
            existing.CreatedByUserId = userId;
        }
    }
}
