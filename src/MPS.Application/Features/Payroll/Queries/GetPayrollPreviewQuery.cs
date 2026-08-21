using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.DTOs.Payroll;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Common;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Queries;

/// <summary>
/// TẠM TÍNH bảng lương của một tháng bằng dữ liệu CÓ ĐẾN THỜI ĐIỂM BẤM — không phải đợi hết tháng.
///
/// <para><b>Chạy đúng engine của kỳ thật.</b> Cùng <see cref="IPayrollDataCollector"/>, cùng
/// <see cref="IPayrollCalculator"/>, cùng <see cref="PayrollPeriodComposer"/> như
/// <c>CreatePayrollPeriodCommand</c> — không có công thức riêng cho bản tạm tính, vì một bản tạm
/// tính lệch với kỳ thật thì tệ hơn là không có.</para>
///
/// <para><b>Tuyệt đối không ghi gì.</b> Kỳ được dựng trong bộ nhớ rồi vứt: không chiếm khoá duy nhất
/// (năm, tháng), không nuốt khoản truy lĩnh/truy thu đang chờ, không đẻ khoản sàn 0 (#44). Các khoản
/// điều chỉnh nạp bằng bản KHÔNG THEO DÕI vì
/// <see cref="PayrollAdjustmentApplier.Build"/> có sửa thẳng entity truyền vào.</para>
///
/// <para><b>Cổng đủ liệu hạ xuống thành cảnh báo.</b> Giữa tháng thì đánh giá tháng chưa ai duyệt và
/// đơn tăng ca còn treo — giữ nguyên cổng chặn thì không bao giờ bấm được. Chỉ NV chưa có hợp đồng
/// ĐÃ KÝ mới bị loại khỏi bảng: không có hợp đồng thì không có mức lương nào để chia, dòng lương sẽ
/// toàn số 0 chứ không phải "tạm tính thấp".</para>
///
/// <para><b>Vì sao con số không bằng "lương nửa tháng" theo trực giác</b> (hệ quả của việc giữ
/// nguyên logic, FE phải nói rõ cho kế toán):
/// <list type="number">
///   <item>Mẫu số <c>CONGCHUAN</c> vẫn là công chuẩn CẢ THÁNG. Bấm ngày 10 thì tử số chỉ có ~7
///         công trên mẫu số ~21 ⇒ ra khoảng 1/3 lương tháng.</item>
///   <item>Thuế TNCN luỹ tiến ăn trên thu nhập một phần tháng nên rơi bậc thấp hơn thực tế ⇒ thuế
///         tạm tính THẤP hơn và thực nhận tạm tính CAO hơn tỷ lệ tương ứng.</item>
///   <item>Phụ cấp có cờ <c>ExcludeFromProration</c> (vd trách nhiệm) trả NGUYÊN, không chia theo
///         ngày công ⇒ bấm ngày 3 vẫn cộng đủ phụ cấp cả tháng.</item>
///   <item>Mức lương lấy theo mốc CUỐI kỳ, nên phụ lục tăng lương hiệu lực ngày 25 đã được phản ánh
///         ngay từ bản tạm tính bấm ngày 10.</item>
/// </list></para>
/// </summary>
/// <param name="StandardWorkingDays">Ghi đè công chuẩn, bỏ trống = số ngày T2–T6 của tháng.</param>
public sealed record GetPayrollPreviewQuery(
    int Year,
    int Month,
    int? FormulaSetId,
    decimal? StandardWorkingDays) : IRequest<PayrollPreviewDto>;

public sealed class GetPayrollPreviewQueryHandler
    : IRequestHandler<GetPayrollPreviewQuery, PayrollPreviewDto>
{
    /// <summary>Trạng thái giả của kỳ tạm tính — không nằm trong <see cref="PayrollPeriodStatus"/>.</summary>
    private const string PreviewStatus = "Preview";

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
    private readonly IClock _clock;

    public GetPayrollPreviewQueryHandler(
        IPayrollPeriodRepository periods, IPayrollFormulaSetRepository formulaSets,
        IPayrollReadinessChecker readiness, IPayrollDataCollector collector,
        IPayrollCalculator calculator, IColumnDependencyGraph graph, IPayrollFieldCatalog catalog,
        IPayrollLawChangeNoteBuilder noteBuilder, IPayrollAdjustmentRepository adjustments,
        ICurrentUserService currentUser, IClock clock)
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
        _clock = clock;
    }

    public async Task<PayrollPreviewDto> Handle(GetPayrollPreviewQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();

        if (request.Month is < 1 or > 12)
            throw new DomainValidationException("Tháng không hợp lệ.");

        var formulaSet = request.FormulaSetId.HasValue
            ? await _formulaSets.GetByIdAsync(request.FormulaSetId.Value, includeColumns: true, ct)
            : await _formulaSets.GetDefaultAsync(includeColumns: true, ct);
        if (formulaSet is null || formulaSet.Columns.Count == 0)
            throw new NotFoundException("Không tìm thấy bảng công thức lương (hoặc bảng chưa có cột).");

        var readiness = await _readiness.CheckAsync(
            new PayrollReadinessRequest(request.Year, request.Month), ct);
        if (readiness.Count == 0)
            throw new DomainValidationException(
                "Không có nhân viên nào cần tính lương trong kỳ (chưa có HĐ hiệu lực).");

        // Chưa có HĐ đã ký ⇒ `PayrollDataCollector` cũng loại HĐ còn Draft, dòng lương sẽ toàn 0 và
        // chỉ làm nhiễu bảng. Tách hẳn ra một khối riêng để kế toán biết ai đang bị treo hồ sơ.
        var excluded = readiness
            .Where(r => r.Missing.Contains(PayrollReadinessBlockers.NoSignedContract))
            .ToList();
        var excludedIds = excluded.Select(r => r.EmployeeInformationId).ToHashSet();
        var included = readiness.Where(r => !excludedIds.Contains(r.EmployeeInformationId)).ToList();
        if (included.Count == 0)
            throw new DomainValidationException(
                "Không có nhân viên nào có hợp đồng đã ký hiệu lực trong kỳ để tạm tính.");

        var includedIds = included.Select(r => r.EmployeeInformationId).ToList();

        var payDate = new DateOnly(
            request.Year, request.Month, DateTime.DaysInMonth(request.Year, request.Month));
        var standardDays = request.StandardWorkingDays
            ?? PayrollWorkingDays.CountWeekdays(request.Year, request.Month);

        var collection = await _collector.CollectAsync(
            new PayrollCollectionRequest(request.Year, request.Month, payDate, standardDays, includedIds), ct);

        var columns = formulaSet.Columns.ToList();
        var plan = PayrollColumnPlan.Build(columns, _graph);
        var now = _clock.UtcNow;

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
        foreach (var note in collection.ComplianceNotes) period.Notes.Add(note);

        // Cùng bộ ghi chú với kỳ thật (trần/ngưỡng + luật đổi so kỳ trước) — nếu thiếu thì khối ghi
        // chú của bản tạm tính nghèo hơn bản chính thức và kế toán tưởng kỳ này không có gì đổi.
        var previous = await _periods.GetPreviousAsync(request.Year, request.Month, ct);
        foreach (var note in _noteBuilder.Build(collection.ComplianceRefs, previous?.ComplianceRefs.ToList()))
            period.Notes.Add(note);

        // ⚠️ AsNoTracking bắt buộc — Applier sửa thẳng các entity này (xem chú thích ở repository).
        var pendingByEmp = (await _adjustments.GetPendingForEmployeesReadOnlyAsync(includedIds, ct))
            .GroupBy(a => a.EmployeeInformationId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PayrollAdjustment>)g.ToList());

        // Kết quả trả về gồm cả khoản cần LƯU của nhánh tạo kỳ — ở đây cố ý VỨT.
        PayrollPeriodComposer.Compose(
            period, collection, plan, _calculator, _catalog, pendingByEmp, userId, now);

        AttachEmployeeNames(period, collection);

        var asOf = ClampToPeriod(VietnamClock.ToVietnamDate(now), request.Year, request.Month);
        var existing = (await _periods.GetAllAsync(request.Year, request.Month, ct)).FirstOrDefault();

        return new PayrollPreviewDto(
            Sheet: BuildSheet(period),
            AsOfDate: asOf,
            ElapsedWorkingDays: PayrollWorkingDays.CountWeekdaysThrough(asOf),
            StandardWorkingDays: standardDays,
            EmployeeCount: includedIds.Count,
            Issues: included.Where(r => !r.IsReady).Select(ToIssue).ToList(),
            Excluded: excluded.Select(ToIssue).ToList(),
            Warnings: readiness[0].Warnings,
            ExistingPeriodId: existing?.Id,
            ExistingPeriodStatus: existing?.Status.ToString());
    }

    /// <summary>
    /// Đóng gói kỳ trong bộ nhớ thành DTO bảng lương, đánh dấu rõ là bản TẠM TÍNH.
    ///
    /// <para>Phải đánh số lại <c>RowId</c>: dòng của kỳ này chưa từng qua DB nên <c>Id</c> đều bằng
    /// 0, mà FE dùng <c>rowId</c> làm React key — để nguyên thì mọi dòng trùng key và bảng render
    /// sai (dòng nhảy chỗ, giữ nguyên nội dung cũ) ngay khi người dùng sort hoặc lọc.</para>
    /// </summary>
    private static DTOs.Payroll.PayrollSheetDto BuildSheet(PayrollPeriod period)
    {
        var sheet = PayrollSheetMapper.Map(period);
        return sheet with
        {
            Id = 0,
            Status = PreviewStatus,
            Rows = sheet.Rows.Select((row, index) => row with { RowId = index + 1 }).ToList(),
        };
    }

    /// <summary>
    /// <c>PayrollSheetMapper</c> lấy tên NV từ navigation <c>PayrollRow.EmployeeInformation</c> —
    /// nhánh kỳ thật có tên vì đọc lại kỳ từ DB kèm Include. Kỳ tạm tính chưa từng qua DB nên phải
    /// gắn tên từ chính liệu đã gom, nếu không cả bảng trống cột "Nhân viên".
    /// </summary>
    private static void AttachEmployeeNames(PayrollPeriod period, PayrollCollectionResult collection)
    {
        var namesById = collection.Employees.ToDictionary(
            e => e.EmployeeInformationId,
            e => e.Texts.GetValueOrDefault(PayrollFieldKeys.EmployeeFullName, string.Empty));

        foreach (var row in period.Rows)
        {
            if (row.EmployeeInformationId is not { } empId) continue;
            if (!namesById.TryGetValue(empId, out var fullName)) continue;

            row.EmployeeInformation = new Domain.Entities.EmployeeInformation { Id = empId, FullName = fullName };
        }
    }

    /// <summary>
    /// Kẹp mốc "đến lúc bấm" vào trong tháng đang xem: xem tháng CŨ thì chốt ở ngày cuối tháng đó
    /// (dữ liệu đã đủ), xem tháng TƯƠNG LAI thì chốt ở ngày 1 (chưa có gì để tính).
    /// </summary>
    private static DateOnly ClampToPeriod(DateOnly today, int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        var end = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        if (today < start) return start;
        return today > end ? end : today;
    }

    private static PayrollPreviewIssueDto ToIssue(PayrollEmployeeReadiness r) =>
        new(r.EmployeeInformationId, r.EmployeeCode, r.FullName, r.Missing);
}
