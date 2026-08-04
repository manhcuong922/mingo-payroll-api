using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Attendance;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Infrastructure.Services;

/// <summary>
/// Sinh 1 kỳ lương DEMO cho seeder — tái dùng đúng engine thật (readiness → collect → calculator →
/// notes) như <c>CreatePayrollPeriodCommandHandler</c>, nhưng nhận <paramref name="userId"/> tường minh
/// (seeder không có <c>ICurrentUserService</c>) và BEST-EFFORT: bỏ qua + cảnh báo nếu kỳ đã tồn tại hoặc
/// còn NV chưa đủ liệu, KHÔNG ném lỗi chặn khởi động app.
/// </summary>
public sealed class PayrollDemoPeriodGenerator
{
    private readonly IPayrollPeriodRepository _periods;
    private readonly IPayrollFormulaSetRepository _formulaSets;
    private readonly IPayrollReadinessChecker _readiness;
    private readonly IPayrollDataCollector _collector;
    private readonly IPayrollCalculator _calculator;
    private readonly IColumnDependencyGraph _graph;
    private readonly IPayrollFieldCatalog _catalog;
    private readonly IPayrollLawChangeNoteBuilder _noteBuilder;
    private readonly IPublicHolidayRepository _holidays;
    private readonly ILogger<PayrollDemoPeriodGenerator> _logger;

    public PayrollDemoPeriodGenerator(
        IPayrollPeriodRepository periods, IPayrollFormulaSetRepository formulaSets,
        IPayrollReadinessChecker readiness, IPayrollDataCollector collector,
        IPayrollCalculator calculator, IColumnDependencyGraph graph, IPayrollFieldCatalog catalog,
        IPayrollLawChangeNoteBuilder noteBuilder, IPublicHolidayRepository holidays,
        ILogger<PayrollDemoPeriodGenerator> logger)
    {
        _periods = periods;
        _formulaSets = formulaSets;
        _readiness = readiness;
        _collector = collector;
        _calculator = calculator;
        _graph = graph;
        _catalog = catalog;
        _noteBuilder = noteBuilder;
        _holidays = holidays;
        _logger = logger;
    }

    /// <summary>Trả về Id kỳ vừa sinh, hoặc null nếu bỏ qua (đã tồn tại / thiếu liệu / không có NV).</summary>
    public async Task<int?> TryGenerateAsync(int year, int month, int userId, CancellationToken ct)
    {
        if (await _periods.ExistsForPeriodAsync(year, month, ct))
        {
            _logger.LogDebug("Payroll demo: kỳ {Month:D2}/{Year} đã tồn tại; bỏ qua.", month, year);
            return null;
        }

        var formulaSet = await _formulaSets.GetDefaultAsync(includeColumns: true, ct);
        if (formulaSet is null || formulaSet.Columns.Count == 0)
        {
            _logger.LogWarning("Payroll demo: chưa có bảng công thức mặc định; bỏ qua kỳ {Month:D2}/{Year}.", month, year);
            return null;
        }

        var readiness = await _readiness.CheckAsync(new PayrollReadinessRequest(year, month), ct);
        if (readiness.Count == 0)
        {
            _logger.LogWarning("Payroll demo: không có NV cần tính lương kỳ {Month:D2}/{Year}.", month, year);
            return null;
        }
        var notReady = readiness.Where(r => !r.IsReady).ToList();
        if (notReady.Count > 0)
        {
            _logger.LogWarning(
                "Payroll demo: {Count} NV chưa đủ liệu kỳ {Month:D2}/{Year} → bỏ qua. Vd: {Sample}",
                notReady.Count, month, year,
                string.Join("; ", notReady.Take(3).Select(r => $"{r.FullName}: {string.Join(", ", r.Missing)}")));
            return null;
        }

        var readyIds = readiness.Select(r => r.EmployeeInformationId).ToList();
        var payDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        // #59: công chuẩn mặc định trừ ngày lễ (lễ luật dương lịch + lễ đã nhập DB).
        var enteredHolidays = PayrollWorkingDays.HolidayDatesInMonth(
            await _holidays.GetByYearAsync(year, includeInactive: false, ct), year, month);
        var standardDays = PayrollWorkingDays.CountStandardWorkingDays(year, month, enteredHolidays);

        var collection = await _collector.CollectAsync(
            new PayrollCollectionRequest(year, month, payDate, standardDays, readyIds), ct);

        var columns = formulaSet.Columns.ToList();
        var plan = PayrollColumnPlan.Build(columns, _graph);
        var now = DateTime.UtcNow;

        var period = new PayrollPeriod
        {
            PeriodYear = year,
            PeriodMonth = month,
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

        var totalRow = new PayrollRow { RowType = PayrollRowType.Total, DisplayOrder = order };
        _calculator.ComputeTotals(plan, employeeRows, totalRow);
        period.Rows.Add(totalRow);

        var previous = await _periods.GetPreviousAsync(year, month, ct);
        foreach (var note in _noteBuilder.Build(collection.ComplianceRefs, previous?.ComplianceRefs.ToList()))
            period.Notes.Add(note);
        foreach (var note in collection.ComplianceNotes) period.Notes.Add(note);

        var id = await _periods.AddAsync(period, ct);
        _logger.LogInformation(
            "Payroll demo: sinh kỳ {Month:D2}/{Year} — {Rows} dòng NV + 1 dòng tổng.", month, year, employeeRows.Count);
        return id;
    }
}
