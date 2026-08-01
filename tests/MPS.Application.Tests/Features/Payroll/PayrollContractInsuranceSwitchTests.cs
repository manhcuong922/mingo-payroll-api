using MPS.Application.Common.Models.Compliance;
using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// A7 — Hợp đồng ghi "không đóng BHYT" nhưng hệ thống vẫn trừ.
///
/// <para>Hợp đồng có <b>3 công tắc bảo hiểm độc lập</b> (BHXH / BHYT / BHTN) ⇒ 8 tổ hợp. Cột cờ
/// <c>FLAGBH</c> là enum thô chỉ biểu diễn được 3 tổ hợp, nên 5 tổ hợp còn lại bị gom nhầm vào
/// "đóng đầy đủ" — điển hình: HĐ ghi <i>có BHXH, KHÔNG BHYT, KHÔNG BHTN</i> vẫn bị trừ đủ cả ba.</para>
///
/// <para>Bản sửa tách hai tầng: <b>công tắc HĐ</b> (sự thật ghi trong hợp đồng, áp cho từng loại
/// bảo hiểm độc lập) và <b>cờ <c>FLAGBH</c></b> (kênh đè tay cho ngoại lệ theo kỳ, giữ nguyên theo
/// <c>[D23]</c>). Test này đo <b>số tiền thật trên bảng lương</b> chứ không đo giá trị cờ — cờ vẫn
/// thô là chủ ý.</para>
/// </summary>
public sealed class PayrollContractInsuranceSwitchTests
{
    private const decimal Salary = 20_000_000m;

    private static decimal Cell(PayrollRow row, string code) =>
        row.Cells.Single(c => c.ColumnCode == code).ComputedValue ?? 0m;

    /// <summary>
    /// Chạy bảng công thức mặc định cho 1 NV. <paramref name="exempt"/> = ba công tắc MIỄN
    /// (BHXH, BHYT, BHTN); <paramref name="flag"/> = cờ BH của kỳ.
    /// </summary>
    private static PayrollRow Compute(
        (bool Social, bool Health, bool Unemployment) exempt,
        string flag = InsuranceFlags.Full)
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
                [PayrollFieldKeys.CongTinhCom] = 23m,
                [PayrollFieldKeys.CongChinhThuc] = 23m,
                [PayrollFieldKeys.ContractMucLuong] = Salary,
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
                [PayrollFieldKeys.ContractNoSocialInsurance] = exempt.Social ? 1m : 0m,
                [PayrollFieldKeys.ContractNoHealthInsurance] = exempt.Health ? 1m : 0m,
                [PayrollFieldKeys.ContractNoUnemploymentInsurance] = exempt.Unemployment ? 1m : 0m,
            },
            Texts =
            {
                [PayrollFieldKeys.ContractInsuranceFlag] = flag,
                [PayrollFieldKeys.ContractTaxFlag] = TaxFlags.Progressive,
            },
        };

        var ctx = new PayrollCalcContext
        {
            Fields = inputs.ToFields(new PayrollFieldCatalog()),
            FunctionContext = new PayrollFunctionContext { PitBrackets = Array.Empty<PitBracketSnapshot>() },
        };

        var row = new PayrollRow { RowType = PayrollRowType.Employee, EmployeeInformationId = 1 };
        calculator.ComputeAll(plan, ctx, row);
        return row;
    }

    /// <summary>Mọi tổ hợp trong 8 tổ hợp phải cho đúng loại nào bị trừ, loại nào không.</summary>
    public static TheoryData<bool, bool, bool> AllEightCombinations()
    {
        var data = new TheoryData<bool, bool, bool>();
        foreach (var social in new[] { false, true })
            foreach (var health in new[] { false, true })
                foreach (var unemployment in new[] { false, true })
                    data.Add(social, health, unemployment);
        return data;
    }

    [Theory(DisplayName = "A7 · Cả 8 tổ hợp công tắc BH trên hợp đồng phải được tôn trọng")]
    [MemberData(nameof(AllEightCombinations))]
    public void EveryContractCombination_IsRespected(bool exemptSocial, bool exemptHealth, bool exemptUnemployment)
    {
        var row = Compute((exemptSocial, exemptHealth, exemptUnemployment));

        AssertZeroOrPositive(row, PayrollColumnCodes.NldBhxh, exemptSocial, "BHXH – NLĐ");
        AssertZeroOrPositive(row, PayrollColumnCodes.DnBhxh, exemptSocial, "BHXH – DN");
        AssertZeroOrPositive(row, PayrollColumnCodes.NldBhyt, exemptHealth, "BHYT – NLĐ");
        AssertZeroOrPositive(row, PayrollColumnCodes.DnBhyt, exemptHealth, "BHYT – DN");
        AssertZeroOrPositive(row, PayrollColumnCodes.NldBhtn, exemptUnemployment, "BHTN – NLĐ");
        AssertZeroOrPositive(row, PayrollColumnCodes.DnBhtn, exemptUnemployment, "BHTN – DN");
    }

    private static void AssertZeroOrPositive(PayrollRow row, string code, bool exempt, string label)
    {
        if (exempt)
            Cell(row, code).Should().Be(0m,
                "{0}: hợp đồng ghi KHÔNG đóng ⇒ không được trừ đồng nào — cờ FLAGBH thô không biểu " +
                "diễn nổi tổ hợp này nên trước đây bị gom vào 'đóng đầy đủ'", label);
        else
            Cell(row, code).Should().BeGreaterThan(0m, "{0}: hợp đồng ghi CÓ đóng ⇒ phải trừ", label);
    }

    [Fact(DisplayName = "A7 · Tổ hợp của báo cáo: có BHXH, không BHYT, không BHTN")]
    public void ReportedCombination_SocialOnly_DeductsOnlySocial()
    {
        var row = Compute((Social: false, Health: true, Unemployment: true));

        Cell(row, PayrollColumnCodes.NldBhxh).Should().BeGreaterThan(0m, "HĐ có BHXH");
        Cell(row, PayrollColumnCodes.NldBhyt).Should().Be(0m, "HĐ ghi KHÔNG đóng BHYT");
        Cell(row, PayrollColumnCodes.NldBhtn).Should().Be(0m, "HĐ ghi KHÔNG đóng BHTN");
    }

    [Fact(DisplayName = "A7 · Cờ đè tay vẫn cắt được cả khối dù hợp đồng bật đủ công tắc")]
    public void ManualFlag_StillOverridesEverything()
    {
        var row = Compute((false, false, false), flag: InsuranceFlags.Maternity);

        Cell(row, PayrollColumnCodes.NldBhxh).Should().Be(0m);
        Cell(row, PayrollColumnCodes.NldBhyt).Should().Be(0m,
            "cờ thai sản 'TS' là kênh ĐÈ TAY theo [D23] — vẫn phải cắt được cả khối bảo hiểm");
        Cell(row, PayrollColumnCodes.NldBhtn).Should().Be(0m);
    }

    [Fact(DisplayName = "A7 · Kỳ lương CŨ (snapshot thiếu 3 khoá mới) vẫn ra đúng số cũ")]
    public void LegacySnapshotWithoutSwitches_KeepsPayingAsBefore()
    {
        // `PayrollEmployeeInputs.ToFields` điền 0 cho mọi khoá thiếu. Vì 3 trường mang ngữ nghĩa
        // "MIỄN" (1 = miễn) nên thiếu khoá ⇒ 0 ⇒ không miễn ⇒ giữ nguyên hành vi cũ. Nếu dùng ngữ
        // nghĩa "có đóng" thì thiếu khoá sẽ XOÁ SẠCH bảo hiểm của mọi kỳ tạo trước bản sửa.
        var legacy = Compute((false, false, false));

        Cell(legacy, PayrollColumnCodes.NldBhxh).Should().BeGreaterThan(0m);
        Cell(legacy, PayrollColumnCodes.NldBhyt).Should().BeGreaterThan(0m);
        Cell(legacy, PayrollColumnCodes.NldBhtn).Should().BeGreaterThan(0m,
            "kỳ lương tạo trước bản sửa không được đổi số chỉ vì snapshot thiếu khoá mới");
    }
}
