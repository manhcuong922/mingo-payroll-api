using System.Globalization;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Commands;

/// <summary>
/// Ghi đè 1 ô rồi tính lại downstream + dòng tổng. Cột số dùng <paramref name="OverrideValue"/>;
/// cột Text (cờ BH) dùng <paramref name="OverrideText"/>. Gỡ override khi cả hai đều rỗng.
/// </summary>
public sealed record EditPayrollCellCommand(
    int PeriodId, int RowId, string ColumnCode, decimal? OverrideValue, string? OverrideText = null) : IRequest;

public sealed class EditPayrollCellCommandHandler : IRequestHandler<EditPayrollCellCommand>
{
    private readonly IPayrollPeriodRepository _periods;
    private readonly IPayrollCalculator _calculator;
    private readonly IColumnDependencyGraph _graph;
    private readonly IPayrollFieldCatalog _catalog;
    private readonly ICurrentUserService _currentUser;

    public EditPayrollCellCommandHandler(
        IPayrollPeriodRepository periods, IPayrollCalculator calculator,
        IColumnDependencyGraph graph, IPayrollFieldCatalog catalog, ICurrentUserService currentUser)
    {
        _periods = periods;
        _calculator = calculator;
        _graph = graph;
        _catalog = catalog;
        _currentUser = currentUser;
    }

    public async Task Handle(EditPayrollCellCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();

        var period = await _periods.GetFullAsync(request.PeriodId, asNoTracking: false, ct)
            ?? throw new NotFoundException($"Không tìm thấy kỳ lương #{request.PeriodId}.");
        if (period.Status != PayrollPeriodStatus.Draft)
            throw new ConflictException("Kỳ lương đã chốt/khoá — không thể sửa.");

        // #62/[D23] — Cột "Cờ thuế" (FLAGTHUE) vẫn do hệ thống suy từ hợp đồng: giữ khoán 20% ("xx")
        // là điểm chết không đạt tới được bằng tay (PIT-K20-01). Cột "Cờ BH" (FLAGBH) thì MỞ cho kế
        // toán đè tay để chữa ngoại lệ bảo hiểm (thai sản "TS", ốm dài ngày) — đúng [D23] "vẫn là Input".
        if (string.Equals(request.ColumnCode, PayrollColumnCodes.FlagThue, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Cột cờ (BH/thuế) do hệ thống suy từ hợp đồng — không cho sửa tay.");

        var defs = PayrollColumnSnapshotMapper.Deserialize(period.FormulaSnapshotJson);
        var plan = PayrollColumnPlan.Build(defs, _graph);
        if (!plan.ByCode.TryGetValue(request.ColumnCode, out var col))
            throw new NotFoundException($"Không có cột '{request.ColumnCode}'.");

        // Cột cờ BH được [D23] cho phép đè tay dù kỳ cũ đã snapshot editable=false.
        var isInsuranceFlag = string.Equals(request.ColumnCode, PayrollColumnCodes.FlagBh, StringComparison.OrdinalIgnoreCase);
        if (!col.IsEditable && !isInsuranceFlag)
            throw new ConflictException($"Cột '{col.Label}' không cho phép sửa tay.");

        var row = period.Rows.FirstOrDefault(r => r.Id == request.RowId && r.RowType == PayrollRowType.Employee)
            ?? throw new NotFoundException($"Không tìm thấy dòng #{request.RowId}.");

        var cell = row.Cells.FirstOrDefault(c => c.ColumnCode == request.ColumnCode);
        if (cell is null)
        {
            cell = new PayrollCellValue { ColumnCode = request.ColumnCode };
            row.Cells.Add(cell);
        }

        var ctx = PayrollRecomputeContext.Build(period, row, _catalog);
        ApplyOverride(cell, col, request, ctx, userId);
        _calculator.Recompute(plan, ctx, row, request.ColumnCode);

        var totalRow = period.Rows.FirstOrDefault(r => r.RowType == PayrollRowType.Total);
        if (totalRow is not null)
        {
            var employeeRows = period.Rows.Where(r => r.RowType == PayrollRowType.Employee).ToList();
            _calculator.ComputeTotals(plan, employeeRows, totalRow);
        }

        period.UpdatedAt = DateTime.UtcNow;
        period.UpdatedByUserId = userId;
        await _periods.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Ghim/gỡ override cho 1 ô. Cột Text (cờ BH) lưu chuỗi cờ vào <see cref="PayrollCellValue.TextValue"/>
    /// để công thức BH (SkipIfFlag) đọc trực tiếp; cột số dùng <see cref="PayrollCellValue.OverrideValue"/>.
    /// Gỡ override (cả hai rỗng) ⇒ khôi phục giá trị máy suy từ hợp đồng.
    /// </summary>
    private static void ApplyOverride(
        PayrollCellValue cell, SalaryColumnDefinition col, EditPayrollCellCommand request,
        PayrollCalcContext ctx, int userId)
    {
        var isText = col.DataType == ColumnDataType.Text;
        var hasOverride = isText
            ? !string.IsNullOrWhiteSpace(request.OverrideText) || request.OverrideValue.HasValue
            : request.OverrideValue.HasValue;

        if (hasOverride)
        {
            cell.IsOverridden = true;
            cell.OverrideValue = request.OverrideValue;
            cell.OverriddenByUserId = userId;
            cell.OverriddenAt = DateTime.UtcNow;
            if (isText)
                cell.TextValue = !string.IsNullOrWhiteSpace(request.OverrideText)
                    ? request.OverrideText!.Trim()
                    : request.OverrideValue!.Value.ToString(CultureInfo.InvariantCulture);
            return;
        }

        cell.IsOverridden = false;
        cell.OverrideValue = null;
        cell.OverriddenByUserId = null;
        cell.OverriddenAt = null;
        if (isText)
            cell.TextValue = col.LookupKey is not null && ctx.Fields.TryGetValue(col.LookupKey, out var f)
                ? f.Text ?? string.Empty
                : string.Empty;
    }
}
