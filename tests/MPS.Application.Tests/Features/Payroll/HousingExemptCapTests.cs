using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// X4 — tiền thuê nhà do DN trả thay chỉ tính vào thu nhập chịu thuế TỐI ĐA 15% tổng thu nhập chịu
/// thuế CHƯA GỒM tiền nhà (TT 111/2013 Đ.11.1); phần vượt được MIỄN.
///
/// <para><b>Lỗi gốc.</b> Bước kẹp này không tồn tại — tiền nhà chảy thẳng vào cột Thu nhập rồi vào
/// TNCT, bị đánh thuế đủ. Đo trên 3 kỳ demo: Trưởng phòng Kinh doanh hưởng 9.000.000đ tiền nhà,
/// trần chỉ khoảng 6,4tr ⇒ ~2,6tr mỗi tháng bị đánh thuế oan.</para>
///
/// <para><b>Ô liên trục.</b> Giám đốc hưởng 8.000.000đ tiền nhà: kỳ 05 và 07 KHÔNG vượt trần, riêng
/// kỳ 06 vượt — vì đó là tháng nghỉ ốm dài, thu nhập tụt thì trần 15% tụt theo. Cùng một con số cố
/// định, hai kỳ đúng một kỳ sai. Test <see cref="LowerIncomeMonth_TipsTheSameAllowanceOverTheCap"/>
/// dựng lại đúng tình huống đó.</para>
/// </summary>
public sealed class HousingExemptCapTests
{
    private const decimal Rate = 0.15m;

    private static decimal Cell(PayrollRow row, string code) =>
        row.Cells.Single(c => c.ColumnCode == code).ComputedValue ?? 0m;

    /// <param name="rate">Tỷ lệ trần dạng phân số. 0 = snapshot kỳ CŨ (không áp trần).</param>
    private static PayrollRow Compute(decimal housing, decimal rate, decimal congLam = 23m)
    {
        var graph = new ColumnDependencyGraph();
        var calculator = new PayrollCalculator(new FormulaEvaluator(new FormulaFunctionRegistry()), graph);

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
                [PayrollFieldKeys.CongTinhCom] = congLam,
                [PayrollFieldKeys.CongChinhThuc] = congLam,
                [PayrollFieldKeys.CongHuongLuong] = congLam,
                [PayrollFieldKeys.ContractMucLuong] = 20_000_000m,
                [PayrollFieldKeys.ContractAllowanceThueNha] = housing,
                [PayrollFieldKeys.HousingExemptRate] = rate,
                [PayrollFieldKeys.BhxhBhytCap] = 50_600_000m,
                [PayrollFieldKeys.BhtnCap] = 106_200_000m,
                [PayrollFieldKeys.PersonalDeduction] = 15_500_000m,
            },
            Texts =
            {
                [PayrollFieldKeys.ContractInsuranceFlag] = InsuranceFlags.Full,
                [PayrollFieldKeys.ContractTaxFlag] = TaxFlags.Progressive,
            },
        };

        var ctx = new PayrollCalcContext
        {
            Fields = inputs.ToFields(new PayrollFieldCatalog()),
            // Một bậc thuế suất 10% cho toàn dải — đủ để chứng minh thuế giảm khi TNCT giảm.
            FunctionContext = new PayrollFunctionContext
            {
                PitBrackets = new[] { new PitBracketSnapshot(1, 0m, null, 0.10m) },
            },
        };

        var row = new PayrollRow { RowType = PayrollRowType.Employee, EmployeeInformationId = 1 };
        calculator.ComputeAll(plan, ctx, row);
        return row;
    }

    [Fact(DisplayName = "X4 · Tiền nhà dưới trần ⇒ không có phần vượt, thu nhập chịu thuế y như trước")]
    public void BelowCap_ChangesNothing()
    {
        // Lương 20tr + trợ cấp lương 20tr ⇒ nền tính trần 40tr ⇒ trần 6tr. Tiền nhà 3tr < trần.
        var row = Compute(housing: 3_000_000m, rate: Rate);

        Cell(row, PayrollColumnCodes.NhaVuotTran).Should().Be(0m);
        Cell(row, PayrollColumnCodes.Tnct).Should().Be(Cell(Compute(3_000_000m, rate: 0m), PayrollColumnCodes.Tnct));
    }

    [Fact(DisplayName = "X4 · Tiền nhà vượt trần ⇒ phần vượt được MIỄN, trừ khỏi thu nhập chịu thuế")]
    public void AboveCap_ExemptsTheExcess()
    {
        var withCap = Compute(housing: 9_000_000m, rate: Rate);
        var legacy = Compute(housing: 9_000_000m, rate: 0m);

        var vuot = Cell(withCap, PayrollColumnCodes.NhaVuotTran);
        vuot.Should().BeGreaterThan(0m, "9tr tiền nhà trên nền ~40tr thì trần chỉ khoảng 6tr");

        (Cell(legacy, PayrollColumnCodes.Tnct) - Cell(withCap, PayrollColumnCodes.Tnct))
            .Should().Be(vuot, "phần vượt trần phải rời khỏi thu nhập chịu thuế đúng bằng chính nó");

        Cell(withCap, PayrollColumnCodes.ThueTncn)
            .Should().BeLessThan(Cell(legacy, PayrollColumnCodes.ThueTncn), "miễn bớt thu nhập thì thuế phải giảm");
    }

    /// <summary>
    /// Trần tính trên thu nhập chịu thuế CHƯA GỒM tiền nhà. Nếu cài nhầm là "15% của TNCT đã gồm nhà"
    /// thì trần bị thổi lên và phần vượt tính thiếu.
    /// </summary>
    [Fact(DisplayName = "X4 · Trần tính trên nền CHƯA GỒM tiền nhà")]
    public void CapBaseExcludesTheHousingItself()
    {
        var row = Compute(housing: 9_000_000m, rate: Rate);

        var nenChuaGomNha = Cell(row, PayrollColumnCodes.Tnct)
            + Cell(row, PayrollColumnCodes.NhaVuotTran)
            - Cell(row, PayrollColumnCodes.ThueNha);

        Cell(row, PayrollColumnCodes.NhaVuotTran)
            .Should().Be(Cell(row, PayrollColumnCodes.ThueNha) - nenChuaGomNha * Rate);
    }

    /// <summary>
    /// ⚠️ Quy ước NGƯỢC với phần lớn field khác: tỷ lệ = 0 phải hiểu là KHÔNG áp trần. Hiểu nhầm
    /// thành "trần bằng 0" sẽ miễn thuế TOÀN BỘ tiền nhà của mọi kỳ lương cũ.
    /// </summary>
    [Fact(DisplayName = "X4 · Snapshot kỳ CŨ (tỷ lệ = 0) ⇒ KHÔNG áp trần, không miễn sạch tiền nhà")]
    public void LegacySnapshot_DoesNotExemptEverything()
    {
        var legacy = Compute(housing: 9_000_000m, rate: 0m);

        Cell(legacy, PayrollColumnCodes.NhaVuotTran).Should().Be(0m);
        Cell(legacy, PayrollColumnCodes.Tnct)
            .Should().BeGreaterThan(Cell(legacy, PayrollColumnCodes.ThueNha),
                "tiền nhà vẫn phải nằm nguyên trong thu nhập chịu thuế như trước bản sửa");
    }

    /// <summary>
    /// Bẫy dấu bắt được lúc đối chiếu trên DB: người nghỉ TRỌN THÁNG có tổng thu nhập 0 nhưng vẫn bị
    /// trừ phụ cấp miễn thuế nguyên tháng ⇒ nền tính trần ÂM ⇒ trần âm ⇒ <c>0 − trần_âm</c> ra số
    /// DƯƠNG, sinh ra "tiền nhà vượt trần" từ hư không cho người không hề hưởng tiền nhà. Một dòng
    /// lương lệch 60.000đ vì đúng chuyện này.
    /// </summary>
    [Fact(DisplayName = "X4 · Không hưởng tiền nhà ⇒ luôn = 0, kể cả khi nền tính trần âm")]
    public void NoHousingAllowance_NeverProducesExcess()
    {
        // Nghỉ trọn tháng: không công, không thu nhập, không tiền nhà.
        var row = Compute(housing: 0m, rate: Rate, congLam: 0m);

        Cell(row, PayrollColumnCodes.NhaVuotTran).Should().Be(0m);
        Cell(row, PayrollColumnCodes.Tnct).Should().BeGreaterThanOrEqualTo(0m,
            "thu nhập chịu thuế không thể âm");
    }

    /// <summary>
    /// Ô liên trục X1a × X4 — dựng lại tình huống Giám đốc kỳ 06: cùng một khoản tiền nhà cố định,
    /// tháng đủ công thì dưới trần, tháng nghỉ dài thì vượt trần vì thu nhập tụt kéo trần tụt theo.
    /// </summary>
    [Fact(DisplayName = "X4 × X1a · Cùng một khoản tiền nhà: đủ công thì dưới trần, thiếu công thì vượt")]
    public void LowerIncomeMonth_TipsTheSameAllowanceOverTheCap()
    {
        const decimal housing = 5_500_000m;

        Cell(Compute(housing, Rate, congLam: 23m), PayrollColumnCodes.NhaVuotTran)
            .Should().Be(0m, "tháng đủ công thu nhập cao nên trần 15% đủ chứa khoản này");

        Cell(Compute(housing, Rate, congLam: 8m), PayrollColumnCodes.NhaVuotTran)
            .Should().BeGreaterThan(0m, "tháng nghỉ dài thu nhập tụt ⇒ trần tụt theo ⇒ chính khoản đó vượt");
    }
}
