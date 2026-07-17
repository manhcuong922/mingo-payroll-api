using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Commands;

/// <summary>Ghi đè (hoặc xoá override khi OverrideValue=null) 1 ô rồi tính lại downstream + dòng tổng.</summary>
public sealed record EditPayrollCellCommand(int PeriodId, int RowId, string ColumnCode, decimal? OverrideValue) : IRequest;

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

        var defs = PayrollColumnSnapshotMapper.Deserialize(period.FormulaSnapshotJson);
        var plan = PayrollColumnPlan.Build(defs, _graph);
        if (!plan.ByCode.TryGetValue(request.ColumnCode, out var col))
            throw new NotFoundException($"Không có cột '{request.ColumnCode}'.");
        if (!col.IsEditable)
            throw new ConflictException($"Cột '{col.Label}' không cho phép sửa tay.");

        var row = period.Rows.FirstOrDefault(r => r.Id == request.RowId && r.RowType == PayrollRowType.Employee)
            ?? throw new NotFoundException($"Không tìm thấy dòng #{request.RowId}.");

        var cell = row.Cells.FirstOrDefault(c => c.ColumnCode == request.ColumnCode);
        if (cell is null)
        {
            cell = new PayrollCellValue { ColumnCode = request.ColumnCode };
            row.Cells.Add(cell);
        }

        if (request.OverrideValue.HasValue)
        {
            cell.IsOverridden = true;
            cell.OverrideValue = request.OverrideValue.Value;
            cell.OverriddenByUserId = userId;
            cell.OverriddenAt = DateTime.UtcNow;
        }
        else
        {
            cell.IsOverridden = false;
            cell.OverrideValue = null;
            cell.OverriddenByUserId = null;
            cell.OverriddenAt = null;
        }

        var ctx = PayrollRecomputeContext.Build(period, row, _catalog);
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
}
