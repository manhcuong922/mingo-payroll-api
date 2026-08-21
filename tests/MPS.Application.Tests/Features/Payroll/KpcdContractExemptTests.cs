using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// HĐ tick "Miễn trích KPCĐ" thì bảng lương phải KHÔNG trích 2% KPCĐ — với mọi tổ hợp bảo hiểm.
///
/// <para><b>Lỗi gốc.</b> Trước bản sửa, "miễn KPCĐ" chỉ tới được engine qua cờ thô <c>FLAGBH</c>.
/// Cờ đó do <c>PayrollDataCollector.ResolveInsuranceFlag</c> suy ra theo thứ tự ưu tiên:
/// HĐ chỉ đóng BHYT trả về <c>x2</c> và <b>return trước</b> khi kịp xét <c>UnionFundExemptFlag</c>.
/// Mà danh sách cắt KPCĐ là <c>[x, x1, TS]</c> — không có <c>x2</c>. Kết quả đo được trên engine
/// (lương 20tr, KPCĐ 2%): cờ <c>x</c>/<c>x1</c>/<c>TS</c> ra 0đ, nhưng cờ <c>x2</c> vẫn ra
/// <b>400.000đ</b>. Đúng 1 trong 8 tổ hợp công tắc BH bị rò, và rò âm thầm.</para>
///
/// <para>Bản sửa tách công tắc HĐ ra khỏi cờ kỳ, đúng khuôn ba cột BH đã làm ở A7 — nên test này
/// đo TIỀN trên bảng lương chứ không đo giá trị cờ.</para>
/// </summary>
public sealed class KpcdContractExemptTests
{
    private const decimal Salary = 20_000_000m;
    private const decimal KpcdRate = 0.02m;
    private const decimal ExpectedKpcd = Salary * KpcdRate;

    private static decimal Cell(PayrollRow row, string code) =>
        row.Cells.Single(c => c.ColumnCode == code).ComputedValue ?? 0m;

    private static PayrollRow Compute(bool unionFundExempt, string flag)
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
                [PayrollFieldKeys.KpcdRate] = KpcdRate,
                [PayrollFieldKeys.BhxhBhytCap] = 46_800_000m,
                [PayrollFieldKeys.BhtnCap] = 106_200_000m,
                [PayrollFieldKeys.PersonalDeduction] = 15_500_000m,
                [PayrollFieldKeys.ContractUnionFundExempt] = unionFundExempt ? 1m : 0m,
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

    /// <summary>Mọi cờ BH mà engine có thể sinh ra — kể cả cờ đè tay của kế toán.</summary>
    public static TheoryData<string> EveryInsuranceFlag()
    {
        var data = new TheoryData<string>();
        foreach (var flag in InsuranceFlags.All) data.Add(flag);
        return data;
    }

    [Theory(DisplayName = "HĐ miễn KPCĐ ⇒ KPCĐ = 0 với MỌI cờ bảo hiểm")]
    [MemberData(nameof(EveryInsuranceFlag))]
    public void ExemptContract_NeverPaysUnionFund(string flag)
    {
        Cell(Compute(unionFundExempt: true, flag), PayrollColumnCodes.Kpcd)
            .Should().Be(0m, $"HĐ ghi miễn KPCĐ nhưng cờ BH của kỳ là '{flag}'");
    }

    [Theory(DisplayName = "HĐ KHÔNG miễn ⇒ vẫn trích KPCĐ ở các cờ còn đóng bảo hiểm")]
    [InlineData(InsuranceFlags.Full)]
    [InlineData(InsuranceFlags.HealthOnly)]
    public void NonExemptContract_StillPaysUnionFund(string flag)
    {
        Cell(Compute(unionFundExempt: false, flag), PayrollColumnCodes.Kpcd)
            .Should().Be(ExpectedKpcd, "không có căn cứ miễn thì DN vẫn nộp KPCĐ theo Luật Công đoàn");
    }

    /// <summary>
    /// Kỳ lương TẠO TRƯỚC bản sửa không có khoá <c>contract.unionFundExempt</c> trong snapshot input.
    /// Thiếu khoá ⇒ <c>ToFields</c> điền 0 ⇒ "không miễn" ⇒ số KPCĐ cũ giữ nguyên. Đây là lý do khoá
    /// mang ngữ nghĩa "MIỄN" chứ không phải "có trích": ngữ nghĩa ngược lại sẽ xoá sạch KPCĐ của mọi
    /// kỳ cũ ngay lần kế toán sửa một ô bất kỳ.
    /// </summary>
    [Fact(DisplayName = "Snapshot kỳ cũ thiếu khoá miễn KPCĐ ⇒ vẫn tính như trước, không xoá số")]
    public void LegacySnapshotWithoutTheKey_KeepsChargingUnionFund()
    {
        var graph = new ColumnDependencyGraph();
        var calculator = new PayrollCalculator(new FormulaEvaluator(new FormulaFunctionRegistry()), graph);
        var columns = DefaultPayrollFormulaSet.BuildColumns();
        foreach (var c in columns) c.FormulaSetId = 1;
        var plan = PayrollColumnPlan.Build(columns, graph);

        // Cố ý KHÔNG đặt ContractUnionFundExempt — mô phỏng snapshot cũ.
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
                [PayrollFieldKeys.KpcdRate] = KpcdRate,
                [PayrollFieldKeys.BhxhBhytCap] = 46_800_000m,
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
            FunctionContext = new PayrollFunctionContext { PitBrackets = Array.Empty<PitBracketSnapshot>() },
        };
        var row = new PayrollRow { RowType = PayrollRowType.Employee, EmployeeInformationId = 1 };
        calculator.ComputeAll(plan, ctx, row);

        Cell(row, PayrollColumnCodes.Kpcd).Should().Be(ExpectedKpcd);
    }
}
