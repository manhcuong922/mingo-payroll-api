using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Commands;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Attendance;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// BS-5 — tạo kỳ lương phải gom các khoản truy lĩnh/truy thu CHƯA áp của NV, đưa vào 2 cột
/// TRUY_LINH/TRUY_THU và đánh dấu khoản đã áp. Dùng engine THẬT (không mock calculator) để kiểm
/// đúng số tiền chảy vào THỰC NHẬN.
/// </summary>
public sealed class CreatePayrollPeriodAdjustmentTests
{
    private const int EmpId = 42;
    private const int SourcePeriodId = 5;

    private readonly Mock<IPayrollPeriodRepository> _periods = new();
    private readonly Mock<IPayrollFormulaSetRepository> _formulaSets = new();
    private readonly Mock<IPayrollReadinessChecker> _readiness = new();
    private readonly Mock<IPayrollDataCollector> _collector = new();
    private readonly Mock<IPayrollLawChangeNoteBuilder> _noteBuilder = new();
    private readonly Mock<IPayrollAdjustmentRepository> _adjustments = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private readonly ColumnDependencyGraph _graph = new();
    private readonly PayrollFieldCatalog _catalog = new();

    private PayrollPeriod? _saved;

    public CreatePayrollPeriodAdjustmentTests()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(2);

        _formulaSets.Setup(f => f.GetDefaultAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollFormulaSet
            {
                Id = 1,
                Name = DefaultPayrollFormulaSet.Name,
                Columns = DefaultPayrollFormulaSet.BuildColumns(),
            });

        _periods.Setup(p => p.ExistsForPeriodAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _periods.Setup(p => p.GetPreviousAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollPeriod?)null);
        _periods.Setup(p => p.AddAsync(It.IsAny<PayrollPeriod>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollPeriod, CancellationToken>((p, _) => _saved = p)
            .ReturnsAsync(11);

        _readiness.Setup(r => r.CheckAsync(It.IsAny<PayrollReadinessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new PayrollEmployeeReadiness(EmpId, "E42", "NV 42", Array.Empty<string>()) });

        _noteBuilder.Setup(n => n.Build(
                It.IsAny<IReadOnlyList<PayrollComplianceRef>>(), It.IsAny<IReadOnlyList<PayrollComplianceRef>?>()))
            .Returns(Array.Empty<PayrollNote>());

        _collector.Setup(c => c.CollectAsync(It.IsAny<PayrollCollectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new PayrollCollectionResult
            {
                Employees = new[] { BuildInputs() },
                ComplianceRefs = Array.Empty<PayrollComplianceRef>(),
                FunctionContext = new PayrollFunctionContext(),
                ProductivitySnapshotJson = "[]",
            });
    }

    // NV không đóng BH, thuế cam kết (0đ) ⇒ THỰC NHẬN = lương gộp, dễ đối chiếu số tròn.
    private static PayrollEmployeeInputs BuildInputs() => new()
    {
        EmployeeInformationId = EmpId,
        Numbers =
        {
            [PayrollFieldKeys.ContractMucLuong] = 10_000_000m,
            [PayrollFieldKeys.ProductivityCoefficient] = 0m,
            [PayrollFieldKeys.CongChuan] = 22m,
            [PayrollFieldKeys.CongChinhThuc] = 22m,
        },
        Texts =
        {
            [PayrollFieldKeys.EmployeeCode] = "E42",
            [PayrollFieldKeys.ContractInsuranceFlag] = InsuranceFlags.NoInsurance,
            [PayrollFieldKeys.ContractTaxFlag] = TaxFlags.CamKet,
        },
    };

    private static PayrollAdjustment Pending(decimal amount) => new()
    {
        Id = 1,
        EmployeeInformationId = EmpId,
        SourcePeriodId = SourcePeriodId,
        Amount = amount,
        Reason = "Duyệt OT sau khi chốt kỳ",
        CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        CreatedByUserId = 2,
    };

    private void SetupPending(params PayrollAdjustment[] pending) =>
        _adjustments.Setup(a => a.GetPendingForEmployeesAsync(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);

    private CreatePayrollPeriodCommandHandler Sut() => new(
        _periods.Object, _formulaSets.Object, _readiness.Object, _collector.Object,
        new PayrollCalculator(new FormulaEvaluator(new FormulaFunctionRegistry()), _graph),
        _graph, _catalog, _noteBuilder.Object, _adjustments.Object, _currentUser.Object);

    private async Task<PayrollRow> CreateAndGetEmployeeRowAsync()
    {
        await Sut().Handle(new CreatePayrollPeriodCommand(2026, 8, null, 22m), CancellationToken.None);
        return _saved!.Rows.Single(r => r.RowType == PayrollRowType.Employee);
    }

    private static decimal Cell(PayrollRow row, string code)
    {
        var cell = row.Cells.Single(c => c.ColumnCode == code);
        return (cell.IsOverridden ? cell.OverrideValue : cell.ComputedValue) ?? 0m;
    }

    [Fact]
    public async Task KhongCoKhoanChuaAp_HaiCotBang0()
    {
        SetupPending();

        var row = await CreateAndGetEmployeeRowAsync();

        Cell(row, PayrollColumnCodes.TruyLinh).Should().Be(0m);
        Cell(row, PayrollColumnCodes.TruyThu).Should().Be(0m);
    }

    [Fact]
    public async Task TruyLinh_VaoCotVaTangThucNhan_DanhDauDaAp()
    {
        SetupPending();
        var baselineNet = Cell(await CreateAndGetEmployeeRowAsync(), PayrollColumnCodes.ThucNhan);

        var arrear = Pending(3_000_000m);
        SetupPending(arrear);

        var row = await CreateAndGetEmployeeRowAsync();

        Cell(row, PayrollColumnCodes.TruyLinh).Should().Be(3_000_000m);
        Cell(row, PayrollColumnCodes.ThucNhan).Should().Be(baselineNet + 3_000_000m);

        // Khoản được gắn vào kỳ mới qua navigation (FK resolve khi EF lưu kỳ).
        arrear.AppliedPeriod.Should().BeSameAs(_saved);
        arrear.AppliedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task TruyThu_LonHonThucNhan_ChanVe0_PhanDuGiuLai()
    {
        SetupPending();
        var baselineNet = Cell(await CreateAndGetEmployeeRowAsync(), PayrollColumnCodes.ThucNhan);

        var clawback = Pending(-(baselineNet + 4_000_000m));
        SetupPending(clawback);
        PayrollAdjustment? carried = null;
        _adjustments.Setup(a => a.Add(It.IsAny<PayrollAdjustment>()))
            .Callback<PayrollAdjustment>(a => carried = a);

        var row = await CreateAndGetEmployeeRowAsync();

        Cell(row, PayrollColumnCodes.ThucNhan).Should().Be(0m, "thực nhận không được âm");
        Cell(row, PayrollColumnCodes.TruyThu).Should().Be(-baselineNet);

        // Phần dư 4tr ở lại chưa áp; phần đã trừ tách thành bản ghi gắn kỳ này.
        clawback.Amount.Should().Be(-4_000_000m);
        clawback.AppliedPeriod.Should().BeNull();
        carried.Should().NotBeNull();
        carried!.Amount.Should().Be(-baselineNet);
        carried.AppliedPeriod.Should().BeSameAs(_saved);
    }

    [Fact]
    public async Task InputSnapshot_LuuSoTruyLinh_DeTinhLaiTaiLapDung()
    {
        SetupPending(Pending(2_500_000m));

        var row = await CreateAndGetEmployeeRowAsync();

        var snapshot = PayrollEmployeeInputs.FromSnapshotJson(EmpId, row.InputSnapshotJson);
        snapshot.Numbers[PayrollFieldKeys.TruyLinh].Should().Be(2_500_000m);
    }
}
