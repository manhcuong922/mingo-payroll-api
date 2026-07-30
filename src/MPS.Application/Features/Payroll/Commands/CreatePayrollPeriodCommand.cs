using MediatR;
using MPS.Application.Common.Exceptions;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Commands;

/// <summary>Tạo kỳ lương: chặn nếu thiếu liệu; snapshot compliance/param/công thức; tính mọi dòng + ghi chú.</summary>
public sealed record CreatePayrollPeriodCommand(
    int Year,
    int Month,
    int? FormulaSetId,
    decimal? StandardWorkingDays) : IRequest<int>;

public sealed class CreatePayrollPeriodCommandHandler : IRequestHandler<CreatePayrollPeriodCommand, int>
{
    private readonly IPayrollPeriodRepository _periods;
    private readonly IPayrollFormulaSetRepository _formulaSets;
    private readonly IPayrollReadinessChecker _readiness;
    private readonly IPayrollDataCollector _collector;
    private readonly IPayrollCalculator _calculator;
    private readonly IColumnDependencyGraph _graph;
    private readonly IPayrollFieldCatalog _catalog;
    private readonly IPayrollLawChangeNoteBuilder _noteBuilder;
    private readonly IPayrollAdjustmentRepository _adjustments;
    private readonly IPublicHolidayRepository _holidays;
    private readonly ICurrentUserService _currentUser;

    public CreatePayrollPeriodCommandHandler(
        IPayrollPeriodRepository periods, IPayrollFormulaSetRepository formulaSets,
        IPayrollReadinessChecker readiness, IPayrollDataCollector collector,
        IPayrollCalculator calculator, IColumnDependencyGraph graph, IPayrollFieldCatalog catalog,
        IPayrollLawChangeNoteBuilder noteBuilder, IPayrollAdjustmentRepository adjustments,
        IPublicHolidayRepository holidays, ICurrentUserService currentUser)
    {
        _periods = periods;
        _formulaSets = formulaSets;
        _readiness = readiness;
        _collector = collector;
        _calculator = calculator;
        _graph = graph;
        _catalog = catalog;
        _noteBuilder = noteBuilder;
        _adjustments = adjustments;
        _holidays = holidays;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreatePayrollPeriodCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();

        if (request.Month is < 1 or > 12)
            throw new DomainValidationException("Tháng không hợp lệ.");

        if (await _periods.ExistsForPeriodAsync(request.Year, request.Month, ct))
            throw new ConflictException($"Kỳ lương {request.Month:D2}/{request.Year} đã tồn tại.");

        var formulaSet = request.FormulaSetId.HasValue
            ? await _formulaSets.GetByIdAsync(request.FormulaSetId.Value, includeColumns: true, ct)
            : await _formulaSets.GetDefaultAsync(includeColumns: true, ct);
        if (formulaSet is null || formulaSet.Columns.Count == 0)
            throw new NotFoundException("Không tìm thấy bảng công thức lương (hoặc bảng chưa có cột).");

        // ===== Cổng đủ liệu — chặn cả kỳ nếu thiếu =====
        var readiness = await _readiness.CheckAsync(
            new PayrollReadinessRequest(request.Year, request.Month), ct);
        if (readiness.Count == 0)
            throw new DomainValidationException("Không có nhân viên nào cần tính lương trong kỳ (chưa có HĐ hiệu lực).");
        var notReady = readiness.Where(r => !r.IsReady).ToList();
        if (notReady.Count > 0)
            throw new PayrollNotReadyException(notReady);

        var readyIds = readiness.Select(r => r.EmployeeInformationId).ToList();

        // ===== Snapshot params/công chuẩn/payDate + gom liệu =====
        var payDate = new DateOnly(request.Year, request.Month, DateTime.DaysInMonth(request.Year, request.Month));
        // #59: công chuẩn mặc định phải TRỪ ngày lễ (lễ luật dương lịch cố định + lễ đã nhập DB).
        // Chỉ nạp lễ khi kế toán KHÔNG tự truyền standardWorkingDays.
        var standardDays = request.StandardWorkingDays
            ?? PayrollWorkingDays.CountStandardWorkingDays(request.Year, request.Month,
                PayrollWorkingDays.HolidayDatesInMonth(
                    await _holidays.GetByYearAsync(request.Year, includeInactive: false, ct),
                    request.Year, request.Month));

        var collection = await _collector.CollectAsync(
            new PayrollCollectionRequest(request.Year, request.Month, payDate, standardDays, readyIds), ct);

        var columns = formulaSet.Columns.ToList();
        var plan = PayrollColumnPlan.Build(columns, _graph);
        var now = DateTime.UtcNow;

        var period = new PayrollPeriod
        {
            PeriodYear = request.Year,
            PeriodMonth = request.Month,
            PayDate = payDate,
            StandardWorkingDays = standardDays,
            Status = PayrollPeriodStatus.Draft,
            FormulaSetId = formulaSet.Id,
            FormulaSnapshotJson = PayrollColumnSnapshotMapper.Serialize(columns),
            ProductivitySnapshotJson = collection.ProductivitySnapshotJson,
            PitBracketsSnapshotJson = PayrollJson.Serialize(collection.FunctionContext.PitBrackets),
            CreatedByUserId = userId,
            CreatedAt = now,
        };
        foreach (var r in collection.ComplianceRefs) period.ComplianceRefs.Add(r);

        // ===== BS-5: khoản truy lĩnh/truy thu chưa áp của các NV trong kỳ =====
        var pendingByEmp = (await _adjustments.GetPendingForEmployeesAsync(readyIds, ct))
            .GroupBy(a => a.EmployeeInformationId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PayrollAdjustment>)g.ToList());

        // ===== Tính từng dòng nhân viên =====
        var ordered = collection.Employees
            .OrderBy(e => e.Texts.GetValueOrDefault(PayrollFieldKeys.EmployeeCode, string.Empty))
            .ToList();

        var employeeRows = new List<PayrollRow>();
        var order = 0;
        foreach (var inputs in ordered)
        {
            var row = new PayrollRow
            {
                RowType = PayrollRowType.Employee,
                EmployeeInformationId = inputs.EmployeeInformationId,
                DisplayOrder = order++,
            };

            var pending = pendingByEmp.GetValueOrDefault(inputs.EmployeeInformationId);

            // Truy lĩnh (+) áp trọn ngay ở lượt tính đầu; truy thu (−) cần biết THỰC NHẬN trước khi
            // trừ (không để âm) nên chỉ chốt ở lượt hai.
            inputs.Numbers[PayrollFieldKeys.TruyLinh] =
                pending is null ? 0m : PayrollAdjustmentApplier.SumPositives(pending);
            inputs.Numbers[PayrollFieldKeys.TruyThu] = 0m;

            var ctx = new PayrollCalcContext
            {
                Fields = inputs.ToFields(_catalog),
                FunctionContext = collection.FunctionContext,
            };
            _calculator.ComputeAll(plan, ctx, row);

            if (pending is not null)
            {
                var netBeforeClawback = EffectiveNumber(row, PayrollColumnCodes.ThucNhan);
                var applyPlan = PayrollAdjustmentApplier.Build(pending, netBeforeClawback, now);

                foreach (var a in applyPlan.Applied) a.AppliedPeriod = period;
                foreach (var c in applyPlan.Carried)
                {
                    c.AppliedPeriod = period;
                    _adjustments.Add(c);
                }

                if (applyPlan.TruyThu != 0m)
                {
                    inputs.Numbers[PayrollFieldKeys.TruyThu] = applyPlan.TruyThu;
                    ctx = new PayrollCalcContext
                    {
                        Fields = inputs.ToFields(_catalog),
                        FunctionContext = collection.FunctionContext,
                    };
                    _calculator.ComputeAll(plan, ctx, row);
                }
            }

            // #44 — sàn 0: sau khi đã chốt truy lĩnh/truy thu, thực nhận vẫn có thể âm (BH tính
            // trên MUC_LUONG nguyên trong khi thu nhập chia theo công chuẩn). Kẹp về 0 và đẩy
            // phần thiếu sang kỳ sau, đúng nguyên tắc đã áp cho truy thu.
            var shortfall = PayrollNetFloor.Apply(row, period, userId, now);
            if (shortfall is not null) _adjustments.Add(shortfall);

            // Snapshot SAU khi đã chốt số truy lĩnh/truy thu — recompute sau này tái lập đúng số.
            row.InputSnapshotJson = inputs.ToSnapshotJson();

            employeeRows.Add(row);
            period.Rows.Add(row);
        }

        // ===== Dòng tổng =====
        var totalRow = new PayrollRow { RowType = PayrollRowType.Total, DisplayOrder = order };
        _calculator.ComputeTotals(plan, employeeRows, totalRow);
        period.Rows.Add(totalRow);

        // ===== Ghi chú: trần/ngưỡng + thay đổi luật so kỳ trước =====
        var previous = await _periods.GetPreviousAsync(request.Year, request.Month, ct);
        foreach (var note in _noteBuilder.Build(collection.ComplianceRefs, previous?.ComplianceRefs.ToList()))
            period.Notes.Add(note);

        // AddAsync lưu trong 1 transaction; các PayrollAdjustment đang tracked (gán AppliedPeriod =
        // period qua navigation) được EF resolve FK và lưu cùng lượt — không có trạng thái nửa vời
        // "kỳ đã có cột truy lĩnh nhưng khoản vẫn chưa đánh dấu áp".
        return await _periods.AddAsync(period, ct);
    }

    private static decimal EffectiveNumber(PayrollRow row, string columnCode)
    {
        var cell = row.Cells.FirstOrDefault(c => c.ColumnCode == columnCode);
        if (cell is null) return 0m;
        return (cell.IsOverridden ? cell.OverrideValue : cell.ComputedValue) ?? 0m;
    }
}
