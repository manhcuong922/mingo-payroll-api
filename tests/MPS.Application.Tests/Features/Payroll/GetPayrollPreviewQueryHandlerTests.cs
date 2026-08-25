using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// Tạm tính bảng lương GIỮA THÁNG (<see cref="GetPayrollPreviewQuery"/>).
///
/// <para>Hai điều phải bảo đảm và cả hai đều dễ vỡ âm thầm: (1) tuyệt đối KHÔNG ghi gì xuống DB —
/// bấm "xem thử" mà nuốt mất khoản truy thu hoặc chiếm chỗ kỳ lương của tháng thì hỏng dữ liệu
/// thật; (2) cổng đủ liệu chỉ còn là cảnh báo — giữa tháng thì đánh giá tháng chưa duyệt và đơn OT
/// còn treo, chặn như kỳ thật thì không ai bấm được.</para>
///
/// <para>Dùng engine THẬT (không mock calculator) để bản tạm tính ra đúng con số của kỳ thật.</para>
/// </summary>
public sealed class GetPayrollPreviewQueryHandlerTests
{
    private const int SignedEmpId = 42;
    private const int DraftContractEmpId = 43;

    private readonly Mock<IPayrollPeriodRepository> _periods = new();
    private readonly Mock<IPayrollFormulaSetRepository> _formulaSets = new();
    private readonly Mock<IPayrollReadinessChecker> _readiness = new();
    private readonly Mock<IPayrollDataCollector> _collector = new();
    private readonly Mock<IPayrollLawChangeNoteBuilder> _noteBuilder = new();
    private readonly Mock<IPayrollAdjustmentRepository> _adjustments = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly IClock _clock =
        Mock.Of<IClock>(c => c.UtcNow == new DateTime(2026, 8, 10, 3, 0, 0, DateTimeKind.Utc));

    private readonly ColumnDependencyGraph _graph = new();
    private readonly PayrollFieldCatalog _catalog = new();

    public GetPayrollPreviewQueryHandlerTests()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(2);

        _formulaSets.Setup(f => f.GetDefaultAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollFormulaSet
            {
                Id = 1,
                Name = DefaultPayrollFormulaSet.Name,
                Columns = DefaultPayrollFormulaSet.BuildColumns(),
            });

        _periods.Setup(p => p.GetAllAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PayrollPeriod>());
        _periods.Setup(p => p.GetPreviousAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollPeriod?)null);

        _noteBuilder.Setup(n => n.Build(
                It.IsAny<IReadOnlyList<PayrollComplianceRef>>(), It.IsAny<IReadOnlyList<PayrollComplianceRef>?>()))
            .Returns(Array.Empty<PayrollNote>());

        SetupReadiness(Ready(SignedEmpId));
        SetupCollected(SignedEmpId);

        _adjustments.Setup(a => a.GetPendingForEmployeesReadOnlyAsync(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PayrollAdjustment>());
    }

    private static PayrollEmployeeReadiness Ready(int empId) =>
        new(empId, $"E{empId}", $"NV {empId}", Array.Empty<string>());

    private static PayrollEmployeeReadiness Missing(int empId, params string[] missing) =>
        new(empId, $"E{empId}", $"NV {empId}", missing);

    private void SetupReadiness(params PayrollEmployeeReadiness[] readiness) =>
        _readiness.Setup(r => r.CheckAsync(It.IsAny<PayrollReadinessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(readiness);

    /// <summary>Collector chỉ trả về đúng những NV mà handler đưa vào — mô phỏng hành vi thật.</summary>
    private void SetupCollected(params int[] empIds) =>
        _collector.Setup(c => c.CollectAsync(It.IsAny<PayrollCollectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollCollectionRequest req, CancellationToken _) => new PayrollCollectionResult
            {
                Employees = empIds.Where(req.EmployeeInformationIds.Contains).Select(BuildInputs).ToList(),
                ComplianceRefs = Array.Empty<PayrollComplianceRef>(),
                FunctionContext = new PayrollFunctionContext(),
                ProductivitySnapshotJson = "[]",
            });

    // NV không đóng BH, thuế cam kết (0đ) ⇒ THỰC NHẬN = lương gộp, dễ đối chiếu số tròn.
    // Đi làm 10/22 công — đúng cảnh "bấm tạm tính vào ngày 10".
    private static PayrollEmployeeInputs BuildInputs(int empId) => new()
    {
        EmployeeInformationId = empId,
        Numbers =
        {
            [PayrollFieldKeys.ContractMucLuong] = 22_000_000m,
            [PayrollFieldKeys.ProductivityCoefficient] = 0m,
            [PayrollFieldKeys.CongChuan] = 22m,
            [PayrollFieldKeys.CongChinhThuc] = 10m,
        },
        Texts =
        {
            [PayrollFieldKeys.EmployeeCode] = $"E{empId}",
            [PayrollFieldKeys.EmployeeFullName] = $"NV {empId}",
            [PayrollFieldKeys.ContractInsuranceFlag] = InsuranceFlags.NoInsurance,
            [PayrollFieldKeys.ContractTaxFlag] = TaxFlags.CamKet,
        },
    };

    private GetPayrollPreviewQueryHandler Sut() => new(
        _periods.Object, _formulaSets.Object, _readiness.Object, _collector.Object,
        new PayrollCalculator(new FormulaEvaluator(new FormulaFunctionRegistry()), _graph),
        _graph, _catalog, _noteBuilder.Object, _adjustments.Object, _currentUser.Object, _clock);

    private Task<MPS.Application.DTOs.Payroll.PayrollPreviewDto> PreviewAsync() =>
        Sut().Handle(new GetPayrollPreviewQuery(2026, 8, null, 22m), CancellationToken.None);

    [Fact]
    public async Task Handle_NeverTouchesDatabase()
    {
        // Arrange — có sẵn một khoản truy thu chờ áp: đây chính là thứ dễ bị nuốt mất.
        _adjustments.Setup(a => a.GetPendingForEmployeesReadOnlyAsync(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PayrollAdjustment
                {
                    Id = 1,
                    EmployeeInformationId = SignedEmpId,
                    Amount = -500_000m,
                    Reason = "Truy thu kỳ trước",
                    CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                    CreatedByUserId = 2,
                },
            });

        // Act
        await PreviewAsync();

        // Assert
        _periods.Verify(p => p.AddAsync(It.IsAny<PayrollPeriod>(), It.IsAny<CancellationToken>()), Times.Never);
        _periods.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _adjustments.Verify(a => a.Add(It.IsAny<PayrollAdjustment>()), Times.Never);
        _adjustments.Verify(a => a.Remove(It.IsAny<PayrollAdjustment>()), Times.Never);
        _adjustments.Verify(a => a.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        // Bản TRACKED không bao giờ được đụng tới — Applier sửa thẳng entity truyền vào.
        _adjustments.Verify(a => a.GetPendingForEmployeesAsync(
            It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ProratesByActualWorkingDays_KeepingFullMonthDenominator()
    {
        // Arrange — 22tr/tháng, công chuẩn 22, đã đi 10 công.

        // Act
        var preview = await PreviewAsync();

        // Assert — mẫu số vẫn là công chuẩn CẢ THÁNG ⇒ 22tr × 10/22 = 10tr.
        var row = preview.Sheet.Rows.Single(r => r.RowType == "Employee");
        var net = row.Cells.Single(c => c.ColumnCode == PayrollColumnCodes.ThucNhan).Value;
        net.Should().Be(10_000_000m);
        preview.StandardWorkingDays.Should().Be(22m);
    }

    [Fact]
    public async Task Handle_MarksSheetAsPreview_NotARealPeriod()
    {
        var preview = await PreviewAsync();

        preview.Sheet.Id.Should().Be(0);
        preview.Sheet.Status.Should().Be("Preview");
        preview.ExistingPeriodId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReportsAsOfDate_InVietnamTime()
    {
        // 2026-08-10 03:00 UTC = 10:00 giờ VN cùng ngày.
        var preview = await PreviewAsync();

        preview.AsOfDate.Should().Be(new DateOnly(2026, 8, 10));
        // T2–T6 từ 01/08 đến hết 10/08/2026: 1/8 là thứ Bảy ⇒ 6 ngày công đã trôi.
        preview.ElapsedWorkingDays.Should().Be(6);
    }

    [Fact]
    public async Task Handle_StillComputes_EmployeeMissingEvaluationOrPendingOvertime()
    {
        // Arrange — đúng cảnh giữa tháng: chưa ai duyệt đánh giá, đơn OT còn treo.
        SetupReadiness(Missing(
            SignedEmpId,
            PayrollReadinessBlockers.NoApprovedEvaluation,
            PayrollReadinessBlockers.PendingOvertime));

        // Act
        var preview = await PreviewAsync();

        // Assert — vẫn có dòng lương, và lý do thiếu được liệt kê để kế toán biết số còn động.
        preview.Sheet.Rows.Should().Contain(r => r.EmployeeInformationId == SignedEmpId);
        preview.EmployeeCount.Should().Be(1);
        preview.Issues.Should().ContainSingle()
            .Which.Missing.Should().Contain(PayrollReadinessBlockers.NoApprovedEvaluation);
        preview.Excluded.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ExcludesEmployeeWithoutSignedContract()
    {
        // Arrange
        SetupReadiness(
            Ready(SignedEmpId),
            Missing(DraftContractEmpId, PayrollReadinessBlockers.NoSignedContract));
        SetupCollected(SignedEmpId, DraftContractEmpId);

        // Act
        var preview = await PreviewAsync();

        // Assert — không có mức lương nào để chia ⇒ tách ra khối riêng thay vì để dòng toàn số 0.
        preview.Sheet.Rows.Should().NotContain(r => r.EmployeeInformationId == DraftContractEmpId);
        preview.EmployeeCount.Should().Be(1);
        preview.Excluded.Should().ContainSingle()
            .Which.EmployeeInformationId.Should().Be(DraftContractEmpId);
    }

    [Fact]
    public async Task Handle_GivesEveryRowAUniqueId_ForReactKeys()
    {
        // Arrange — 2 NV + dòng tổng: entity trong bộ nhớ có Id đều bằng 0.
        SetupReadiness(Ready(SignedEmpId), Ready(DraftContractEmpId));
        SetupCollected(SignedEmpId, DraftContractEmpId);

        // Act
        var preview = await PreviewAsync();

        // Assert
        var ids = preview.Sheet.Rows.Select(r => r.RowId).ToList();
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().NotContain(0);
    }

    [Fact]
    public async Task Handle_CarriesEmployeeName_WhenPeriodNeverHitTheDatabase()
    {
        var preview = await PreviewAsync();

        preview.Sheet.Rows.Single(r => r.RowType == "Employee")
            .EmployeeName.Should().Be($"NV {SignedEmpId}");
    }
}
