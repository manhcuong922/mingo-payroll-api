using MediatR;
using MPS.Application.Common.Exceptions;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Commands;

/// <summary>
/// BS-6/[D1] — tính lại kỳ lương Draft từ NGUỒN MỚI (chấm công/OT/nghỉ/phụ lục/đánh giá đã đổi),
/// GIỮ NGUYÊN các ô đã ghim tay (override), rồi xoá cờ stale. Công thức vẫn dùng snapshot đã đóng
/// băng lúc tạo kỳ — chỉ số liệu nguồn được làm mới, không để công thức đổi lén.
/// </summary>
public sealed record RecalculatePayrollPeriodCommand(int PeriodId) : IRequest;

public sealed class RecalculatePayrollPeriodCommandHandler : IRequestHandler<RecalculatePayrollPeriodCommand>
{
    private readonly IPayrollPeriodRepository _periods;
    private readonly IPayrollReadinessChecker _readiness;
    private readonly IPayrollDataCollector _collector;
    private readonly IPayrollCalculator _calculator;
    private readonly IColumnDependencyGraph _graph;
    private readonly IPayrollFieldCatalog _catalog;
    private readonly ICurrentUserService _currentUser;

    public RecalculatePayrollPeriodCommandHandler(
        IPayrollPeriodRepository periods, IPayrollReadinessChecker readiness,
        IPayrollDataCollector collector, IPayrollCalculator calculator,
        IColumnDependencyGraph graph, IPayrollFieldCatalog catalog, ICurrentUserService currentUser)
    {
        _periods = periods;
        _readiness = readiness;
        _collector = collector;
        _calculator = calculator;
        _graph = graph;
        _catalog = catalog;
        _currentUser = currentUser;
    }

    public async Task Handle(RecalculatePayrollPeriodCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();

        var period = await _periods.GetFullAsync(request.PeriodId, asNoTracking: false, ct)
            ?? throw new NotFoundException($"Không tìm thấy kỳ lương #{request.PeriodId}.");
        if (period.Status != PayrollPeriodStatus.Draft)
            throw new ConflictException("Chỉ tính lại được kỳ đang ở trạng thái Nháp.");

        // Đủ liệu lại từ đầu — nguồn đổi không được để kỳ rơi vào trạng thái thiếu liệu âm thầm.
        var readiness = await _readiness.CheckAsync(
            new PayrollReadinessRequest(period.PeriodYear, period.PeriodMonth), ct);
        var notReady = readiness.Where(r => !r.IsReady).ToList();
        if (notReady.Count > 0)
            throw new PayrollNotReadyException(notReady);

        var readyIds = readiness.Select(r => r.EmployeeInformationId).ToList();

        // MVP: tính lại = làm mới SỐ LIỆU cho đúng tập nhân viên hiện có. Tập NV đổi (tuyển/nghỉ giữa
        // kỳ) không nằm trong 4 nguồn stale nên hiếm; nếu lệch, yêu cầu xoá + tạo lại kỳ cho tường minh.
        var existingEmpIds = period.Rows
            .Where(r => r.RowType == PayrollRowType.Employee && r.EmployeeInformationId.HasValue)
            .Select(r => r.EmployeeInformationId!.Value)
            .ToHashSet();
        if (!existingEmpIds.SetEquals(readyIds))
            throw new ConflictException(
                "Danh sách nhân viên đủ liệu đã khác lúc tạo kỳ (có người vào/ra giữa kỳ). "
                + "Hãy xoá kỳ và tạo lại để đảm bảo đúng người.");

        var collection = await _collector.CollectAsync(
            new PayrollCollectionRequest(
                period.PeriodYear, period.PeriodMonth, period.PayDate, period.StandardWorkingDays, readyIds), ct);

        var defs = PayrollColumnSnapshotMapper.Deserialize(period.FormulaSnapshotJson);
        var plan = PayrollColumnPlan.Build(defs, _graph);

        // Ghim tay hiện có (empId, cột) → giữ nguyên qua lần tính lại.
        var pinned = SnapshotPinnedOverrides(period);

        var rowByEmp = period.Rows
            .Where(r => r.RowType == PayrollRowType.Employee && r.EmployeeInformationId.HasValue)
            .ToDictionary(r => r.EmployeeInformationId!.Value);

        var ordered = collection.Employees
            .OrderBy(e => e.Texts.GetValueOrDefault(PayrollFieldKeys.EmployeeCode, string.Empty))
            .ToList();

        var employeeRows = new List<PayrollRow>();
        var order = 0;
        foreach (var inputs in ordered)
        {
            var row = rowByEmp[inputs.EmployeeInformationId];
            row.DisplayOrder = order++;

            // BS-5: truy lĩnh/truy thu đã được chốt lúc TẠO kỳ (và các khoản đã đánh dấu áp vào kỳ
            // này). Collector không biết chúng ⇒ phải khôi phục từ snapshot cũ, nếu không lần tính
            // lại sẽ âm thầm xoá số truy lĩnh khỏi bảng lương.
            CarryOverAdjustmentFields(row.InputSnapshotJson, inputs);
            row.InputSnapshotJson = inputs.ToSnapshotJson();

            var ctx = new PayrollCalcContext
            {
                Fields = inputs.ToFields(_catalog),
                FunctionContext = collection.FunctionContext,
            };

            // ComputeAll xoá sạch cells rồi tính lại từ số liệu mới (không giữ override).
            _calculator.ComputeAll(plan, ctx, row);

            // Áp lại từng ô ghim rồi lan xuống downstream — đúng ngữ nghĩa EditPayrollCell.
            if (pinned.TryGetValue(inputs.EmployeeInformationId, out var pins))
                ReapplyPins(plan, ctx, row, pins);

            employeeRows.Add(row);
        }

        var totalRow = period.Rows.FirstOrDefault(r => r.RowType == PayrollRowType.Total);
        if (totalRow is not null)
            _calculator.ComputeTotals(plan, employeeRows, totalRow);

        // Làm mới snapshot phụ thuộc số liệu (hệ số năng suất). Công thức + compliance giữ frozen.
        period.ProductivitySnapshotJson = collection.ProductivitySnapshotJson;

        period.IsStale = false;
        period.StaleReason = null;
        period.StaleMarkedAt = null;
        period.UpdatedAt = DateTime.UtcNow;
        period.UpdatedByUserId = userId;

        await _periods.SaveChangesAsync(ct);
    }

    /// <summary>Giữ nguyên 2 field truy lĩnh/truy thu từ snapshot cũ sang bộ input vừa gom lại.</summary>
    private static void CarryOverAdjustmentFields(string? oldSnapshotJson, PayrollEmployeeInputs newInputs)
    {
        if (string.IsNullOrEmpty(oldSnapshotJson)) return;

        var old = PayrollEmployeeInputs.FromSnapshotJson(newInputs.EmployeeInformationId, oldSnapshotJson);
        foreach (var key in new[] { PayrollFieldKeys.TruyLinh, PayrollFieldKeys.TruyThu })
        {
            if (old.Numbers.TryGetValue(key, out var value)) newInputs.Numbers[key] = value;
        }
    }

    private static Dictionary<int, List<PinnedCell>> SnapshotPinnedOverrides(PayrollPeriod period)
    {
        var result = new Dictionary<int, List<PinnedCell>>();
        foreach (var row in period.Rows)
        {
            if (row.RowType != PayrollRowType.Employee || row.EmployeeInformationId is not int empId) continue;
            var pins = row.Cells
                .Where(c => c.IsOverridden && c.OverrideValue.HasValue)
                .Select(c => new PinnedCell(c.ColumnCode, c.OverrideValue!.Value, c.OverriddenByUserId, c.OverriddenAt))
                .ToList();
            if (pins.Count > 0) result[empId] = pins;
        }
        return result;
    }

    private void ReapplyPins(
        PayrollColumnPlan plan, PayrollCalcContext ctx, PayrollRow row, List<PinnedCell> pins)
    {
        foreach (var pin in pins)
        {
            if (!plan.ByCode.TryGetValue(pin.ColumnCode, out var col) || !col.IsEditable) continue;

            var cell = row.Cells.FirstOrDefault(c => c.ColumnCode == pin.ColumnCode);
            if (cell is null)
            {
                cell = new PayrollCellValue { ColumnCode = pin.ColumnCode };
                row.Cells.Add(cell);
            }

            cell.IsOverridden = true;
            cell.OverrideValue = pin.Value;
            cell.OverriddenByUserId = pin.OverriddenByUserId;
            cell.OverriddenAt = pin.OverriddenAt;

            _calculator.Recompute(plan, ctx, row, pin.ColumnCode);
        }
    }

    private sealed record PinnedCell(string ColumnCode, decimal Value, int? OverriddenByUserId, DateTime? OverriddenAt);
}
