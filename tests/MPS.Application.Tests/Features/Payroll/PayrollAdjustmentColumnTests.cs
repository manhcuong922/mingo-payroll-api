using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// BS-5 — 2 cột TRUY_LINH / TRUY_THU trên bảng công thức mặc định phải cộng đại số vào THỰC NHẬN,
/// và KHÔNG được chạm thu nhập chịu thuế (phương án (A): thuế đã tính đúng ở kỳ phát sinh).
/// </summary>
public sealed class PayrollAdjustmentColumnTests
{
    private static decimal Cell(PayrollRow row, string code)
        => row.Cells.Single(c => c.ColumnCode == code).ComputedValue ?? 0m;

    private static PayrollRow Compute(decimal truyLinh, decimal truyThu)
    {
        var graph = new ColumnDependencyGraph();
        var calculator = new PayrollCalculator(new FormulaEvaluator(new FormulaFunctionRegistry()), graph);
        var catalog = new PayrollFieldCatalog();
        var plan = PayrollColumnPlan.Build(DefaultPayrollFormulaSet.BuildColumns(), graph);

        var inputs = new PayrollEmployeeInputs
        {
            EmployeeInformationId = 1,
            Numbers =
            {
                [PayrollFieldKeys.ContractMucLuong] = 10_000_000m,
                [PayrollFieldKeys.ProductivityCoefficient] = 1m,
                [PayrollFieldKeys.CongChuan] = 22m,
                [PayrollFieldKeys.CongChinhThuc] = 22m,
                [PayrollFieldKeys.PersonalDeduction] = 11_000_000m,
                [PayrollFieldKeys.DependentDeduction] = 4_400_000m,
                [PayrollFieldKeys.TruyLinh] = truyLinh,
                [PayrollFieldKeys.TruyThu] = truyThu,
            },
            Texts =
            {
                [PayrollFieldKeys.ContractInsuranceFlag] = InsuranceFlags.NoInsurance,
                [PayrollFieldKeys.ContractTaxFlag] = TaxFlags.Progressive,
            },
        };

        var ctx = new PayrollCalcContext
        {
            Fields = inputs.ToFields(catalog),
            FunctionContext = new PayrollFunctionContext
            {
                PitBrackets = new List<PitBracketSnapshot> { new(1, 0m, null, 0.05m) },
            },
        };

        var row = new PayrollRow();
        calculator.ComputeAll(plan, ctx, row);
        return row;
    }

    [Fact]
    public void KhongCoKhoanTruyLinh_ThucNhanGiuNguyen()
    {
        var baseline = Compute(truyLinh: 0m, truyThu: 0m);

        Cell(baseline, PayrollColumnCodes.TruyLinh).Should().Be(0m);
        Cell(baseline, PayrollColumnCodes.TruyThu).Should().Be(0m);
    }

    [Fact]
    public void TruyLinh_CongThangVaoThucNhan()
    {
        var baseline = Compute(0m, 0m);
        var withArrear = Compute(truyLinh: 2_000_000m, truyThu: 0m);

        Cell(withArrear, PayrollColumnCodes.ThucNhan).Should()
            .Be(Cell(baseline, PayrollColumnCodes.ThucNhan) + 2_000_000m);
    }

    [Fact]
    public void TruyThu_TruThangKhoiThucNhan()
    {
        var baseline = Compute(0m, 0m);
        var withClawback = Compute(truyLinh: 0m, truyThu: -1_500_000m);

        Cell(withClawback, PayrollColumnCodes.ThucNhan).Should()
            .Be(Cell(baseline, PayrollColumnCodes.ThucNhan) - 1_500_000m);
    }

    [Fact]
    public void TruyLinh_KhongLamDoiThueTncn_PhuongAnA()
    {
        var baseline = Compute(0m, 0m);
        var withArrear = Compute(truyLinh: 9_000_000m, truyThu: -3_000_000m);

        // Thuế + thu nhập chịu thuế của kỳ mới KHÔNG đổi — khoản truy lĩnh đã chịu thuế ở kỳ cũ.
        Cell(withArrear, PayrollColumnCodes.Tnct).Should().Be(Cell(baseline, PayrollColumnCodes.Tnct));
        Cell(withArrear, PayrollColumnCodes.ThueTncn).Should().Be(Cell(baseline, PayrollColumnCodes.ThueTncn));
        Cell(withArrear, PayrollColumnCodes.TongThuNhap).Should().Be(Cell(baseline, PayrollColumnCodes.TongThuNhap));
    }
}
