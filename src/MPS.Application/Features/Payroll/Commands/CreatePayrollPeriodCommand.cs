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
    private readonly ICurrentUserService _currentUser;

    public CreatePayrollPeriodCommandHandler(
        IPayrollPeriodRepository periods, IPayrollFormulaSetRepository formulaSets,
        IPayrollReadinessChecker readiness, IPayrollDataCollector collector,
        IPayrollCalculator calculator, IColumnDependencyGraph graph, IPayrollFieldCatalog catalog,
        IPayrollLawChangeNoteBuilder noteBuilder, ICurrentUserService currentUser)
    {
        _periods = periods;
        _formulaSets = formulaSets;
        _readiness = readiness;
        _collector = collector;
        _calculator = calculator;
        _graph = graph;
        _catalog = catalog;
        _noteBuilder = noteBuilder;
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
        var standardDays = request.StandardWorkingDays ?? PayrollWorkingDays.CountWeekdays(request.Year, request.Month);

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
            ParamsSnapshotJson = collection.ParamsSnapshotJson,
            ProductivitySnapshotJson = collection.ProductivitySnapshotJson,
            PitBracketsSnapshotJson = PayrollJson.Serialize(collection.FunctionContext.PitBrackets),
            CreatedByUserId = userId,
            CreatedAt = now,
        };
        foreach (var r in collection.ComplianceRefs) period.ComplianceRefs.Add(r);

        // ===== Tính từng dòng nhân viên =====
        var inputsByEmp = collection.Employees.ToDictionary(e => e.EmployeeInformationId);
        var ordered = collection.Employees
            .OrderBy(e => e.Texts.GetValueOrDefault(PayrollFieldKeys.EmployeeCode, string.Empty))
            .ToList();

        var employeeRows = new List<PayrollRow>();
        var order = 0;
        foreach (var inputs in ordered)
        {
            var ctx = new PayrollCalcContext
            {
                Fields = inputs.ToFields(_catalog),
                Params = collection.ParamValues,
                FunctionContext = collection.FunctionContext,
            };
            var row = new PayrollRow
            {
                RowType = PayrollRowType.Employee,
                EmployeeInformationId = inputs.EmployeeInformationId,
                DisplayOrder = order++,
                InputSnapshotJson = inputs.ToSnapshotJson(),
            };
            _calculator.ComputeAll(plan, ctx, row);
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

        return await _periods.AddAsync(period, ct);
    }
}
