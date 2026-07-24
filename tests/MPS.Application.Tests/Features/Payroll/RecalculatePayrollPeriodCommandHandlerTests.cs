using FluentAssertions;
using Moq;
using MPS.Application.Common.Exceptions;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Commands;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// BS-6/[D1] — tính lại kỳ Draft từ nguồn mới, giữ ô ghim, xoá cờ stale. Kiểm phần điều phối
/// (calculator được mock — thuật toán tính đã có test riêng).
/// </summary>
public sealed class RecalculatePayrollPeriodCommandHandlerTests
{
    private const int PeriodId = 7;
    private const int EmpId = 42;

    private readonly Mock<IPayrollPeriodRepository> _periods = new();
    private readonly Mock<IPayrollReadinessChecker> _readiness = new();
    private readonly Mock<IPayrollDataCollector> _collector = new();
    private readonly Mock<IPayrollCalculator> _calculator = new();
    private readonly Mock<IColumnDependencyGraph> _graph = new();
    private readonly Mock<IPayrollFieldCatalog> _catalog = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IPayrollAdjustmentRepository> _adjustments = new();

    public RecalculatePayrollPeriodCommandHandlerTests()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(2);
        _catalog.Setup(c => c.GetAll()).Returns(Array.Empty<PayrollFieldDescriptor>());
        _graph.Setup(g => g.TopologicalOrder(It.IsAny<IReadOnlyDictionary<string, FormulaNode?>>()))
            .Returns(Array.Empty<string>());
    }

    private RecalculatePayrollPeriodCommandHandler Sut() => new(
        _periods.Object, _readiness.Object, _collector.Object, _calculator.Object,
        _graph.Object, _catalog.Object, _currentUser.Object, _adjustments.Object);

    private static PayrollPeriod DraftPeriodWithOneEmployee(bool stale)
    {
        var period = new PayrollPeriod
        {
            Id = PeriodId,
            PeriodYear = 2026,
            PeriodMonth = 6,
            PayDate = new DateOnly(2026, 6, 30),
            StandardWorkingDays = 21m,
            Status = PayrollPeriodStatus.Draft,
            IsStale = stale,
            StaleReason = stale ? "nguồn đổi" : null,
            StaleMarkedAt = stale ? DateTime.UtcNow : null,
            FormulaSnapshotJson = PayrollColumnSnapshotMapper.Serialize(Array.Empty<SalaryColumnDefinition>()),
        };
        period.Rows.Add(new PayrollRow
        {
            RowType = PayrollRowType.Employee,
            EmployeeInformationId = EmpId,
            DisplayOrder = 0,
        });
        period.Rows.Add(new PayrollRow { RowType = PayrollRowType.Total, DisplayOrder = 1 });
        return period;
    }

    private void SetupReady(params int[] empIds) =>
        _readiness.Setup(r => r.CheckAsync(It.IsAny<PayrollReadinessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(empIds.Select(id => new PayrollEmployeeReadiness(id, $"E{id}", $"NV {id}", Array.Empty<string>())).ToList());

    private void SetupCollectFor(params int[] empIds) =>
        _collector.Setup(c => c.CollectAsync(It.IsAny<PayrollCollectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollCollectionResult
            {
                Employees = empIds.Select(id => new PayrollEmployeeInputs { EmployeeInformationId = id }).ToList(),
                ComplianceRefs = Array.Empty<PayrollComplianceRef>(),
                FunctionContext = new PayrollFunctionContext(),
                ProductivitySnapshotJson = "[]",
            });

    [Fact]
    public async Task Recalculate_ClearsStaleFlag_AndRecomputesRows()
    {
        var period = DraftPeriodWithOneEmployee(stale: true);
        _periods.Setup(p => p.GetFullAsync(PeriodId, false, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        SetupReady(EmpId);
        SetupCollectFor(EmpId);

        await Sut().Handle(new RecalculatePayrollPeriodCommand(PeriodId), CancellationToken.None);

        period.IsStale.Should().BeFalse();
        period.StaleReason.Should().BeNull();
        period.StaleMarkedAt.Should().BeNull();
        _calculator.Verify(c => c.ComputeAll(
            It.IsAny<PayrollColumnPlan>(), It.IsAny<PayrollCalcContext>(),
            It.Is<PayrollRow>(r => r.EmployeeInformationId == EmpId)), Times.Once);
        _periods.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Recalculate_Throws_WhenPeriodFinalized()
    {
        var period = DraftPeriodWithOneEmployee(stale: true);
        period.Status = PayrollPeriodStatus.Finalized;
        _periods.Setup(p => p.GetFullAsync(PeriodId, false, It.IsAny<CancellationToken>())).ReturnsAsync(period);

        var act = () => Sut().Handle(new RecalculatePayrollPeriodCommand(PeriodId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _periods.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Recalculate_Throws_WhenAnEmployeeNotReady()
    {
        var period = DraftPeriodWithOneEmployee(stale: true);
        _periods.Setup(p => p.GetFullAsync(PeriodId, false, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _readiness.Setup(r => r.CheckAsync(It.IsAny<PayrollReadinessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new PayrollEmployeeReadiness(EmpId, "E42", "NV 42", new[] { "Thiếu chấm công" }) });

        var act = () => Sut().Handle(new RecalculatePayrollPeriodCommand(PeriodId), CancellationToken.None);

        await act.Should().ThrowAsync<PayrollNotReadyException>();
    }

    [Fact]
    public async Task Recalculate_GiuNguyenTruyLinhTruyThu_BS5()
    {
        // Kỳ đã có khoản truy lĩnh chốt lúc tạo. Collector KHÔNG biết field này (nó chỉ gom nguồn
        // chấm công/HĐ) ⇒ nếu không carry-over, lần tính lại sẽ âm thầm xoá 2tr khỏi bảng lương.
        var period = DraftPeriodWithOneEmployee(stale: true);
        var empRow = period.Rows.First(r => r.RowType == PayrollRowType.Employee);
        empRow.InputSnapshotJson = new PayrollEmployeeInputs
        {
            EmployeeInformationId = EmpId,
            Numbers =
            {
                [PayrollFieldKeys.TruyLinh] = 2_000_000m,
                [PayrollFieldKeys.TruyThu] = -500_000m,
            },
        }.ToSnapshotJson();

        _periods.Setup(p => p.GetFullAsync(PeriodId, false, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        SetupReady(EmpId);
        SetupCollectFor(EmpId);   // input mới KHÔNG có 2 field này

        await Sut().Handle(new RecalculatePayrollPeriodCommand(PeriodId), CancellationToken.None);

        var after = PayrollEmployeeInputs.FromSnapshotJson(EmpId, empRow.InputSnapshotJson);
        after.Numbers[PayrollFieldKeys.TruyLinh].Should().Be(2_000_000m);
        after.Numbers[PayrollFieldKeys.TruyThu].Should().Be(-500_000m);
    }

    [Fact]
    public async Task Recalculate_Throws_WhenEligibleEmployeeSetChanged()
    {
        var period = DraftPeriodWithOneEmployee(stale: true);
        _periods.Setup(p => p.GetFullAsync(PeriodId, false, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        SetupReady(EmpId, 99);   // có thêm NV mới đủ liệu ⇒ tập lệch

        var act = () => Sut().Handle(new RecalculatePayrollPeriodCommand(PeriodId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _periods.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
