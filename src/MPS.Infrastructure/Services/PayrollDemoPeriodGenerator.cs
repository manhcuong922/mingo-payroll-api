using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Constants;
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
///
/// <para><b>Phải bám sát handler thật.</b> Mọi khối tính tiền của handler đều phải có mặt ở đây —
/// gồm BS-5 (gom truy lĩnh/truy thu chưa áp) và #44 (sàn 0 thực nhận). Thiếu một khối thì kỳ lương
/// DEMO được sinh theo quy tắc khác kỳ kế toán tạo qua giao diện, và dữ liệu mẫu mô tả sai chính sản
/// phẩm — nguy hiểm hơn hẳn việc thiếu vài con số.</para>
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
    private readonly IPayrollAdjustmentRepository _adjustments;
    private readonly ILogger<PayrollDemoPeriodGenerator> _logger;

    public PayrollDemoPeriodGenerator(
        IPayrollPeriodRepository periods, IPayrollFormulaSetRepository formulaSets,
        IPayrollReadinessChecker readiness, IPayrollDataCollector collector,
        IPayrollCalculator calculator, IColumnDependencyGraph graph, IPayrollFieldCatalog catalog,
        IPayrollLawChangeNoteBuilder noteBuilder,
        IPayrollAdjustmentRepository adjustments, ILogger<PayrollDemoPeriodGenerator> logger)
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
        // ⭐ CC-2026-08 — dùng ĐÚNG quy tắc của `CreatePayrollPeriodCommand`: công chuẩn = ngày công
        // chế độ của tháng (T2–T6), KHÔNG trừ ngày lễ; ngày lễ được cộng vào tử số ở collector.
        // Kỳ demo phải khớp kỳ thật, nếu không dữ liệu demo sẽ dạy sai cách đọc bảng lương.
        var standardDays = PayrollWorkingDays.CountWeekdays(year, month);

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
            PieceWorkSupportSnapshotJson = collection.PieceWorkSupportSnapshotJson,
            PitBracketsSnapshotJson = PayrollJson.Serialize(collection.FunctionContext.PitBrackets),
            CreatedByUserId = userId,
            CreatedAt = now,
        };
        foreach (var r in collection.ComplianceRefs) period.ComplianceRefs.Add(r);

        // BS-5 — khoản truy lĩnh/truy thu CHƯA áp của các NV trong kỳ. Phải gom ở đây thì hai cột
        // TRUY_LINH/TRUY_THU của kỳ demo mới có số, và các khoản mới được đánh dấu là đã áp.
        var pendingByEmp = (await _adjustments.GetPendingForEmployeesAsync(readyIds, ct))
            .GroupBy(a => a.EmployeeInformationId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PayrollAdjustment>)g.ToList());

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
            // trừ (không để âm) nên chỉ chốt được ở lượt hai.
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

            // #44 — sàn 0 thực nhận: phần thiếu đẩy sang kỳ sau thay vì trả số âm.
            var shortfall = PayrollNetFloor.Apply(row, period, userId, now);
            if (shortfall is not null) _adjustments.Add(shortfall);

            // Snapshot SAU khi đã chốt truy lĩnh/truy thu — tính lại về sau mới ra đúng số.
            row.InputSnapshotJson = inputs.ToSnapshotJson();

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

    /// <summary>Giá trị đang có hiệu lực của một ô: số kế toán sửa tay thắng số engine tính.</summary>
    private static decimal EffectiveNumber(PayrollRow row, string columnCode)
    {
        var cell = row.Cells.FirstOrDefault(c => c.ColumnCode == columnCode);
        if (cell is null) return 0m;
        return (cell.IsOverridden ? cell.OverrideValue : cell.ComputedValue) ?? 0m;
    }
}
