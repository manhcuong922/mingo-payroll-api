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
    private readonly ICurrentUserService _currentUser;

    public CreatePayrollPeriodCommandHandler(
        IPayrollPeriodRepository periods, IPayrollFormulaSetRepository formulaSets,
        IPayrollReadinessChecker readiness, IPayrollDataCollector collector,
        IPayrollCalculator calculator, IColumnDependencyGraph graph, IPayrollFieldCatalog catalog,
        IPayrollLawChangeNoteBuilder noteBuilder, IPayrollAdjustmentRepository adjustments,
        ICurrentUserService currentUser)
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
        // ⭐ CC-2026-08 — công chuẩn mặc định = NGÀY CÔNG CHẾ ĐỘ của tháng (mọi ngày trừ T7/CN),
        // KHÔNG trừ ngày lễ nữa (đảo lại #59/HOL-STD01).
        //
        // Lý do: CONGCHUAN là MẪU SỐ của mọi phép chia lương. Trừ lễ ra khiến mẫu số co lại trong
        // tháng nhiều lễ (tháng Tết còn 17 thay vì 22) ⇒ mỗi ngày nghỉ bị trừ 1/17 thay vì 1/22,
        // tức cùng một hành vi nghỉ mà tháng Tết bị phạt nặng hơn tháng thường ~30%. Đơn giá ngày
        // công phải ổn định giữa các tháng.
        //
        // Bù lại, ngày lễ được ghi nhận là NGÀY CÔNG HƯỞNG LƯƠNG ở tử số (Đ.112 BLLĐ — nghỉ lễ
        // hưởng nguyên lương), xem `PayrollDataCollector` mục "bù công ngày lễ". Hai vế đi cùng
        // nhau: chỉ đổi mẫu số mà quên tử số ⇒ NLĐ mất trắng lương những ngày lễ.
        var standardDays = request.StandardWorkingDays
            ?? PayrollWorkingDays.CountWeekdays(request.Year, request.Month);

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
            PieceWorkSupportSnapshotJson = collection.PieceWorkSupportSnapshotJson,
            PitBracketsSnapshotJson = PayrollJson.Serialize(collection.FunctionContext.PitBrackets),
            CreatedByUserId = userId,
            CreatedAt = now,
        };
        foreach (var r in collection.ComplianceRefs) period.ComplianceRefs.Add(r);

        // ===== BS-5: khoản truy lĩnh/truy thu chưa áp của các NV trong kỳ =====
        var pendingByEmp = (await _adjustments.GetPendingForEmployeesAsync(readyIds, ct))
            .GroupBy(a => a.EmployeeInformationId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PayrollAdjustment>)g.ToList());

        // ===== Tính từng dòng nhân viên + dòng tổng =====
        // Dùng chung với màn hình TẠM TÍNH giữa tháng — xem PayrollPeriodComposer.
        var composition = PayrollPeriodComposer.Compose(
            period, collection, plan, _calculator, _catalog, pendingByEmp, userId, now);

        foreach (var adjustment in composition.CarriedAdjustments) _adjustments.Add(adjustment);
        foreach (var adjustment in composition.ShortfallAdjustments) _adjustments.Add(adjustment);

        // ===== Ghi chú: trần/ngưỡng + thay đổi luật so kỳ trước =====
        var previous = await _periods.GetPreviousAsync(request.Year, request.Month, ct);
        foreach (var note in _noteBuilder.Build(collection.ComplianceRefs, previous?.ComplianceRefs.ToList()))
            period.Notes.Add(note);

        // ⭐ [Mục 1] Ghi chú luật đổi NGAY TRONG KỲ — bộ diff ở trên chỉ so với kỳ trước nên không thấy.
        foreach (var note in collection.ComplianceNotes) period.Notes.Add(note);

        // AddAsync lưu trong 1 transaction; các PayrollAdjustment đang tracked (gán AppliedPeriod =
        // period qua navigation) được EF resolve FK và lưu cùng lượt — không có trạng thái nửa vời
        // "kỳ đã có cột truy lĩnh nhưng khoản vẫn chưa đánh dấu áp".
        return await _periods.AddAsync(period, ct);
    }
}
