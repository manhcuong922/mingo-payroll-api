using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Commands;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// ⭐ CC-2026-08 — công chuẩn mặc định của kỳ lương là <b>NGÀY CÔNG CHẾ ĐỘ</b> của tháng
/// (mọi ngày trừ T7/CN), <b>KHÔNG trừ ngày lễ</b>. Đảo lại quy tắc #59/HOL-STD01 cũ.
///
/// <para>Lý do: CONGCHUAN là MẪU SỐ của mọi phép chia lương. Trừ lễ ra làm mẫu số co lại trong
/// tháng nhiều lễ (tháng Tết còn 17 thay vì 22) ⇒ nghỉ 1 ngày bị trừ 1/17 thay vì 1/22, tức cùng
/// một hành vi nghỉ mà tháng Tết bị phạt nặng hơn tháng thường ~30%.</para>
///
/// <para>Vế còn lại (ngày lễ được cộng vào TỬ SỐ) nằm ở
/// <c>MPS.Infrastructure.Tests.Services.PayrollCollectorHolidayCreditTests</c>. Hai vế phải đi cùng
/// nhau — chỉ đổi mẫu số thì NLĐ mất trắng lương ngày lễ.</para>
/// </summary>
public sealed class CreatePayrollPeriodStandardDaysTests
{
    private const int EmpId = 42;

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

    /// <summary>Công chuẩn mà collector NHẬN được — phải khớp số ghi vào kỳ.</summary>
    private decimal? _collectedStandardDays;

    public CreatePayrollPeriodStandardDaysTests()
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

        _adjustments.Setup(a => a.GetPendingForEmployeesAsync(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PayrollAdjustment>());

        _collector.Setup(c => c.CollectAsync(It.IsAny<PayrollCollectionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollCollectionRequest, CancellationToken>((r, _) => _collectedStandardDays = r.StandardWorkingDays)
            .ReturnsAsync(() => new PayrollCollectionResult
            {
                Employees = new[] { BuildInputs() },
                ComplianceRefs = Array.Empty<PayrollComplianceRef>(),
                FunctionContext = new PayrollFunctionContext(),
                ProductivitySnapshotJson = "[]",
            });
    }

    private static PayrollEmployeeInputs BuildInputs() => new()
    {
        EmployeeInformationId = EmpId,
        Numbers =
        {
            [PayrollFieldKeys.ContractMucLuong] = 10_000_000m,
            [PayrollFieldKeys.ProductivityCoefficient] = 0m,
        },
        Texts =
        {
            [PayrollFieldKeys.EmployeeCode] = "E42",
            [PayrollFieldKeys.ContractInsuranceFlag] = InsuranceFlags.NoInsurance,
            [PayrollFieldKeys.ContractTaxFlag] = TaxFlags.CamKet,
        },
    };

    private CreatePayrollPeriodCommandHandler Sut() => new(
        _periods.Object, _formulaSets.Object, _readiness.Object, _collector.Object,
        new PayrollCalculator(new FormulaEvaluator(new FormulaFunctionRegistry()), _graph),
        _graph, _catalog, _noteBuilder.Object, _adjustments.Object, _currentUser.Object);

    private async Task<decimal> CreateAsync(int year, int month, decimal? standardDays = null)
    {
        await Sut().Handle(new CreatePayrollPeriodCommand(year, month, null, standardDays), CancellationToken.None);
        return _saved!.StandardWorkingDays;
    }

    [Theory(DisplayName = "CC-SWD01 · Công chuẩn mặc định KHÔNG trừ ngày lễ dương lịch cố định")]
    [InlineData(2026, 1)] // Tết Dương lịch 01/01 — Thứ Năm
    [InlineData(2026, 4)] // Giải phóng 30/04 — Thứ Năm
    [InlineData(2026, 5)] // Quốc tế Lao động 01/05 — Thứ Sáu
    [InlineData(2026, 9)] // Quốc khánh 02/09 — Thứ Tư
    public async Task Default_StandardDays_Does_Not_Subtract_Statutory_Holiday(int year, int month)
    {
        var actual = await CreateAsync(year, month);

        actual.Should().Be(PayrollWorkingDays.CountWeekdays(year, month),
            "ngày lễ nay nằm ở TỬ SỐ (công hưởng lương), không còn bị trừ khỏi mẫu số");
    }

    [Fact(DisplayName = "CC-SWD02 · Công chuẩn mặc định = số ngày T2–T6 của tháng")]
    public async Task Default_StandardDays_Equals_Weekday_Count()
    {
        // Tháng 6/2026 không có mốc lễ dương lịch nào ⇒ quy tắc cũ và mới cho cùng một số,
        // dùng làm mốc đối chiếu rằng phép đếm nền vẫn là "ngày T2–T6".
        (await CreateAsync(2026, 6)).Should().Be(PayrollWorkingDays.CountWeekdays(2026, 6));
    }

    [Fact(DisplayName = "CC-SWD03 · Kế toán truyền tay công chuẩn thì hệ thống tôn trọng")]
    public async Task Explicit_StandardDays_Wins_Over_Default()
    {
        (await CreateAsync(2026, 1, standardDays: 20m)).Should().Be(20m);
    }

    [Fact(DisplayName = "CC-SWD04 · Công chuẩn ghi vào kỳ và công chuẩn đưa cho collector là MỘT")]
    public async Task Collector_Receives_Same_StandardDays_As_Period()
    {
        // Hai số này là mẫu số của cùng một phép chia (income phân bổ theo ngày công) — lệch nhau
        // thì bảng lương và đơn giá giờ dùng hai mẫu số khác nhau.
        var saved = await CreateAsync(2026, 1);

        _collectedStandardDays.Should().Be(saved);
    }
}
