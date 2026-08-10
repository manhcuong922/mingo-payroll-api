using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// BS-2 phần A — 2 cột PHUTROI_NGAYNGHI / PHUCAP_DEM trên bảng công thức mặc định phải:
/// (1) cộng vào TỔNG THU NHẬP (⇒ tăng THỰC NHẬN), và
/// (2) được trừ khỏi THU NHẬP CHỊU THUẾ — miễn thuế TNCN như cột "Tăng ca miễn thuế" (chốt PO).
/// </summary>
public sealed class PayrollPremiumColumnTests
{
    private static decimal Cell(PayrollRow row, string code)
        => row.Cells.Single(c => c.ColumnCode == code).ComputedValue ?? 0m;

    private static PayrollRow Compute(decimal premiumDayType, decimal premiumNight)
    {
        var graph = new ColumnDependencyGraph();
        var calculator = new PayrollCalculator(new FormulaEvaluator(new FormulaFunctionRegistry()), graph);
        var plan = PayrollColumnPlan.Build(DefaultPayrollFormulaSet.BuildColumns(), graph);

        var inputs = new PayrollEmployeeInputs
        {
            EmployeeInformationId = 1,
            Numbers =
            {
                [PayrollFieldKeys.ContractMucLuong] = 20_000_000m,
                [PayrollFieldKeys.ProductivityCoefficient] = 0m,
                [PayrollFieldKeys.CongChuan] = 22m,
                [PayrollFieldKeys.CongChinhThuc] = 22m,
                [PayrollFieldKeys.PersonalDeduction] = 11_000_000m,
                [PayrollFieldKeys.DependentDeduction] = 4_400_000m,
                [PayrollFieldKeys.PremiumDayType] = premiumDayType,
                [PayrollFieldKeys.PremiumNight] = premiumNight,
            },
            Texts =
            {
                [PayrollFieldKeys.ContractInsuranceFlag] = InsuranceFlags.NoInsurance,
                [PayrollFieldKeys.ContractTaxFlag] = TaxFlags.Progressive,
            },
        };

        var ctx = new PayrollCalcContext
        {
            Fields = inputs.ToFields(new PayrollFieldCatalog()),
            FunctionContext = new PayrollFunctionContext
            {
                PitBrackets = new List<PitBracketSnapshot> { new(1, 0m, null, 0.10m) },
            },
        };

        var row = new PayrollRow();
        calculator.ComputeAll(plan, ctx, row);
        return row;
    }

    [Fact]
    public void KhongLamNgayNghiHayCaDem_HaiCotBang0()
    {
        var row = Compute(0m, 0m);

        Cell(row, PayrollColumnCodes.PhuTroiNgayNghi).Should().Be(0m);
        Cell(row, PayrollColumnCodes.PhuCapDem).Should().Be(0m);
    }

    [Fact]
    public void PhuTroiVaPhuCapDem_CongVaoTongThuNhap()
    {
        var baseline = Compute(0m, 0m);
        var withPremium = Compute(premiumDayType: 1_200_000m, premiumNight: 300_000m);

        Cell(withPremium, PayrollColumnCodes.TongThuNhap).Should()
            .Be(Cell(baseline, PayrollColumnCodes.TongThuNhap) + 1_500_000m);
    }

    [Fact]
    public void PhuTroiVaPhuCapDem_MienThue_KhongLamTangTnct()
    {
        var baseline = Compute(0m, 0m);
        var withPremium = Compute(premiumDayType: 1_200_000m, premiumNight: 300_000m);

        // Cộng vào tổng thu nhập nhưng trừ lại ở TNCT ⇒ thu nhập chịu thuế + thuế KHÔNG đổi.
        Cell(withPremium, PayrollColumnCodes.Tnct).Should().Be(Cell(baseline, PayrollColumnCodes.Tnct));
        Cell(withPremium, PayrollColumnCodes.ThueTncn).Should().Be(Cell(baseline, PayrollColumnCodes.ThueTncn));
    }

    [Fact]
    public void PhuTroi_VaoThucNhan_DayDuKhongBiTruThue()
    {
        var baseline = Compute(0m, 0m);
        var withPremium = Compute(premiumDayType: 1_200_000m, premiumNight: 300_000m);

        // Miễn thuế ⇒ NLĐ nhận trọn 1.5tr, không bị hao vì thuế luỹ tiến.
        Cell(withPremium, PayrollColumnCodes.ThucNhan).Should()
            .Be(Cell(baseline, PayrollColumnCodes.ThucNhan) + 1_500_000m);
    }
}
