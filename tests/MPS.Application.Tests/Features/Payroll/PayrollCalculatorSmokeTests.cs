using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// Smoke test cho engine + bảng công thức mặc định: tính 1 NV official, lương 20tr, hệ số 1.0,
/// đối chiếu số tính tay đến THỰC NHẬN. Cũng kiểm tra explanation trace cột Cơm.
/// </summary>
public sealed class PayrollCalculatorSmokeTests
{
    private static decimal Cell(PayrollRow row, string code)
        => row.Cells.Single(c => c.ColumnCode == code).ComputedValue ?? 0m;

    [Fact]
    public void ComputeAll_DefaultFormulaSet_MatchesHandComputedTakeHome()
    {
        // Arrange
        var registry = new FormulaFunctionRegistry();
        var evaluator = new FormulaEvaluator(registry);
        var graph = new ColumnDependencyGraph();
        var calculator = new PayrollCalculator(evaluator, graph);
        var catalog = new PayrollFieldCatalog();

        var columns = DefaultPayrollFormulaSet.BuildColumns();
        foreach (var c in columns) c.FormulaSetId = 1;
        var plan = PayrollColumnPlan.Build(columns, graph);

        var inputs = new PayrollEmployeeInputs
        {
            EmployeeInformationId = 1,
            Numbers =
            {
                [PayrollFieldKeys.ProductivityCoefficient] = 1.0m,
                [PayrollFieldKeys.CongChuan] = 23m,
                [PayrollFieldKeys.CongTinhCom] = 20m,
                [PayrollFieldKeys.CongThuViec] = 0m,
                [PayrollFieldKeys.CongChinhThuc] = 23m,
                [PayrollFieldKeys.CongHocViec] = 0m,
                [PayrollFieldKeys.ContractMucLuong] = 20_000_000m,
                [PayrollFieldKeys.DependentsCount] = 1m,
                [PayrollFieldKeys.BhxhDnRate] = 0.175m,
                [PayrollFieldKeys.BhxhNldRate] = 0.08m,
                [PayrollFieldKeys.BhytDnRate] = 0.03m,
                [PayrollFieldKeys.BhytNldRate] = 0.015m,
                [PayrollFieldKeys.BhtnDnRate] = 0.01m,
                [PayrollFieldKeys.BhtnNldRate] = 0.01m,
                [PayrollFieldKeys.KpcdRate] = 0.02m,
                [PayrollFieldKeys.BhxhBhytCap] = 46_800_000m,
                [PayrollFieldKeys.BhtnCap] = 106_200_000m,
                [PayrollFieldKeys.PersonalDeduction] = 15_500_000m,
                [PayrollFieldKeys.DependentDeduction] = 6_200_000m,
                [PayrollFieldKeys.MealExemptThreshold] = 730_000m,
            },
            Texts =
            {
                [PayrollFieldKeys.EmployeeCode] = "E1",
                [PayrollFieldKeys.ContractInsuranceFlag] = InsuranceFlags.Full,
                [PayrollFieldKeys.ContractTaxFlag] = TaxFlags.Progressive,
            },
        };

        var ctx = new PayrollCalcContext
        {
            Fields = inputs.ToFields(catalog),
            Params = new Dictionary<string, ResolvedValue>
            {
                [PayrollParameterKeys.MealPricePerWorkday] = new(40_000m, "Đơn giá cơm/công"),
            },
            FunctionContext = new PayrollFunctionContext
            {
                PitBrackets = new List<PitBracketSnapshot>
                {
                    new(1, 0m, 10_000_000m, 0.05m),
                    new(2, 10_000_000m, 30_000_000m, 0.10m),
                    new(3, 30_000_000m, 60_000_000m, 0.20m),
                    new(4, 60_000_000m, 100_000_000m, 0.30m),
                    new(5, 100_000_000m, null, 0.35m),
                },
            },
        };

        var row = new PayrollRow();

        // Act
        calculator.ComputeAll(plan, ctx, row);

        // Assert — đối chiếu số tính tay
        Cell(row, PayrollColumnCodes.PhuCapLuong).Should().Be(20_000_000m);
        Cell(row, PayrollColumnCodes.Com).Should().Be(800_000m);
        Cell(row, PayrollColumnCodes.ThuNhap).Should().Be(40_800_000m);
        Cell(row, PayrollColumnCodes.TongThuNhap).Should().Be(40_800_000m);
        Cell(row, PayrollColumnCodes.CongBhNld).Should().Be(2_100_000m);
        Cell(row, PayrollColumnCodes.Tntt).Should().Be(16_270_000m);
        Cell(row, PayrollColumnCodes.ThueTncn).Should().Be(1_127_000m);
        Cell(row, PayrollColumnCodes.ThucNhan).Should().Be(37_573_000m);
    }

    [Fact]
    public void ComputeAll_MealColumn_ProducesReadableExplanation()
    {
        var graph = new ColumnDependencyGraph();
        var calculator = new PayrollCalculator(new FormulaEvaluator(new FormulaFunctionRegistry()), graph);
        var catalog = new PayrollFieldCatalog();
        var columns = DefaultPayrollFormulaSet.BuildColumns();
        var plan = PayrollColumnPlan.Build(columns, graph);

        var inputs = new PayrollEmployeeInputs
        {
            EmployeeInformationId = 1,
            Numbers = { [PayrollFieldKeys.CongTinhCom] = 10m },
            Texts = { [PayrollFieldKeys.ContractInsuranceFlag] = InsuranceFlags.Full },
        };
        var ctx = new PayrollCalcContext
        {
            Fields = inputs.ToFields(catalog),
            Params = new Dictionary<string, ResolvedValue> { [PayrollParameterKeys.MealPricePerWorkday] = new(20_000m, "Giá cơm 1 bữa") },
            FunctionContext = new PayrollFunctionContext(),
        };
        var row = new PayrollRow();

        calculator.ComputeAll(plan, ctx, row);

        var com = row.Cells.Single(c => c.ColumnCode == PayrollColumnCodes.Com);
        com.ComputedValue.Should().Be(200_000m);
        com.ExplanationJson.Should().NotBeNull();
        var explanation = PayrollJson.Deserialize<ExplanationNode>(com.ExplanationJson!)!;
        explanation.Detail.Should().Contain("10").And.Contain("20.000").And.Contain("200.000");
    }
}
