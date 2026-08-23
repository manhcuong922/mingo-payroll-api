using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// X1 — trần đóng bảo hiểm chặn MỨC LƯƠNG THÁNG, việc chia theo ngày công là bước SAU (QĐ 595).
///
/// <para><b>Lỗi gốc.</b> Engine làm ngược: <c>MIN(nền_đã_chia_theo_công, trần) × tỷ lệ</c>. Chỉ có
/// MỘT trục biến động thì không lộ — người đủ công, lương không đổi vẫn ra nền đúng bằng trần.
/// Ghép trục thứ hai mới sai, và sai theo hướng THU THỪA:</para>
/// <list type="bullet">
///   <item><b>X1a</b> — Giám đốc lương 55tr nghỉ ốm 14 ngày, còn 8/22 công. Engine chia trước ra nền
///   20.000.000, thấy chưa chạm trần 46,8tr nên thu đủ. Đúng ra <c>46,8tr × 8/22 = 17.018.182</c>.
///   Chênh 2.981.818 tiền nền ⇒ NLĐ +238.545, DN +521.818.</item>
///   <item><b>X1b</b> — nâng bậc 45tr → 55tr từ 16/05. Engine bình quân ra 49.761.905 rồi mới kẹp
///   còn 46.800.000. Đúng ra kẹp từng đoạn rồi bình quân: <c>(11×45tr + 10×46,8tr)/21 = 45.857.143</c>.
///   </item>
/// </list>
///
/// <para>Trục kẹp-theo-ngày nằm ở collector (<c>contract.mucLuongCap*</c>); test này đo phần bảng
/// công thức: nền đã kẹp có được TÔN TRỌNG không, và kỳ cũ thiếu khoá có giữ nguyên số không.</para>
/// </summary>
public sealed class InsuranceCapOrderTests
{
    private const decimal Cap = 46_800_000m;
    private const decimal Salary = 55_000_000m;
    private const decimal BhxhNldRate = 0.08m;
    private const decimal BhxhDnRate = 0.175m;

    private static decimal Cell(PayrollRow row, string code) =>
        row.Cells.Single(c => c.ColumnCode == code).ComputedValue ?? 0m;

    /// <param name="cappedBase">Nền tháng đã kẹp trần theo ngày. 0 = snapshot kỳ CŨ.</param>
    private static PayrollRow Compute(decimal congChuan, decimal congLam, decimal cappedBase)
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
                [PayrollFieldKeys.CongChuan] = congChuan,
                [PayrollFieldKeys.CongTinhCom] = congLam,
                [PayrollFieldKeys.CongChinhThuc] = congLam,
                [PayrollFieldKeys.CongHuongLuong] = congLam,
                [PayrollFieldKeys.ContractMucLuong] = Salary,
                [PayrollFieldKeys.ContractMucLuongCapBhxh] = cappedBase,
                [PayrollFieldKeys.BhxhNldRate] = BhxhNldRate,
                [PayrollFieldKeys.BhxhDnRate] = BhxhDnRate,
                [PayrollFieldKeys.BhxhBhytCap] = Cap,
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
        return row;
    }

    [Fact(DisplayName = "X1a — thiếu công trên trần: nền là TRẦN chia theo công, không phải lương chia theo công")]
    public void ShortMonth_ProratesTheCapNotTheSalary()
    {
        // Giám đốc: 8/22 công. Collector đã kẹp trần từng ngày ⇒ nền tháng = 46.800.000.
        var row = Compute(congChuan: 22m, congLam: 8m, cappedBase: Cap);

        // Cột BH làm tròn về đồng, chia ngược cho 8% khuếch đại sai số làm tròn lên 12,5 lần ⇒ dung sai 15đ.
        var nen = Cell(row, PayrollColumnCodes.NldBhxh) / BhxhNldRate;
        nen.Should().BeApproximately(Cap * 8m / 22m, 15m, "trần chặn mức lương THÁNG, chia công là bước sau");
        nen.Should().BeApproximately(17_018_181.82m, 15m);
    }

    [Fact(DisplayName = "X1a — số cũ (lương chia theo công) là số SAI, phải khác hẳn")]
    public void ShortMonth_OldBehaviourWasDifferent()
    {
        var fixedRow = Compute(congChuan: 22m, congLam: 8m, cappedBase: Cap);
        var legacyRow = Compute(congChuan: 22m, congLam: 8m, cappedBase: 0m);

        var legacyNen = Cell(legacyRow, PayrollColumnCodes.NldBhxh) / BhxhNldRate;
        legacyNen.Should().BeApproximately(Salary * 8m / 22m, 15m, "kỳ cũ vẫn chia lương 55tr rồi mới so trần");

        var thuThua = Cell(legacyRow, PayrollColumnCodes.NldBhxh) - Cell(fixedRow, PayrollColumnCodes.NldBhxh);
        thuThua.Should().BeApproximately(238_545.45m, 0.5m, "phần NLĐ bị thu thừa mỗi tháng");

        (Cell(legacyRow, PayrollColumnCodes.DnBhxh) - Cell(fixedRow, PayrollColumnCodes.DnBhxh))
            .Should().BeApproximately(521_818.18m, 0.5m, "phần DN bị thu thừa mỗi tháng");
    }

    [Fact(DisplayName = "X1b — nâng bậc giữa kỳ: kẹp từng đoạn rồi bình quân, không bình quân rồi mới kẹp")]
    public void MidMonthRaise_CapsEachSegmentBeforeAveraging()
    {
        // 11 công ở 45tr (dưới trần, giữ nguyên) + 10 công ở 55tr (kẹp còn 46,8tr), chia 21 công.
        const decimal dungLuat = (11m * 45_000_000m + 10m * Cap) / 21m;

        var row = Compute(congChuan: 21m, congLam: 21m, cappedBase: dungLuat);

        var nen = Cell(row, PayrollColumnCodes.NldBhxh) / BhxhNldRate;
        nen.Should().BeApproximately(45_857_142.86m, 15m);
        nen.Should().BeLessThan(Cap, "bình quân của một đoạn dưới trần và một đoạn bằng trần thì phải THẤP hơn trần");
    }

    [Fact(DisplayName = "Đối chứng — đủ công, lương không đổi: nền đúng bằng trần, y như trước")]
    public void FullMonthOnCap_IsUnchanged()
    {
        var fixedRow = Compute(congChuan: 23m, congLam: 23m, cappedBase: Cap);
        var legacyRow = Compute(congChuan: 23m, congLam: 23m, cappedBase: 0m);

        Cell(fixedRow, PayrollColumnCodes.NldBhxh).Should().Be(Cap * BhxhNldRate);

        Cell(fixedRow, PayrollColumnCodes.NldBhxh).Should().Be(Cell(legacyRow, PayrollColumnCodes.NldBhxh),
            "chỉ một trục biến động thì engine cũ vốn đã đúng — bản sửa không được đổi số ở đây");
    }

    [Fact(DisplayName = "Đối chứng — lương dưới trần: bản sửa không đụng tới số")]
    public void BelowCap_IsUnchanged()
    {
        const decimal small = 8_000_000m;

        var graph = new ColumnDependencyGraph();
        var calculator = new PayrollCalculator(new FormulaEvaluator(new FormulaFunctionRegistry()), graph);
        var columns = DefaultPayrollFormulaSet.BuildColumns();
        foreach (var c in columns) c.FormulaSetId = 1;
        var plan = PayrollColumnPlan.Build(columns, graph);

        decimal Run(decimal cappedBase)
        {
            var inputs = new PayrollEmployeeInputs
            {
                EmployeeInformationId = 1,
                Numbers =
                {
                    [PayrollFieldKeys.ProductivityCoefficient] = 1.0m,
                    [PayrollFieldKeys.CongChuan] = 22m,
                    [PayrollFieldKeys.CongTinhCom] = 10m,
                    [PayrollFieldKeys.CongChinhThuc] = 10m,
                    [PayrollFieldKeys.CongHuongLuong] = 10m,
                    [PayrollFieldKeys.ContractMucLuong] = small,
                    [PayrollFieldKeys.ContractMucLuongCapBhxh] = cappedBase,
                    [PayrollFieldKeys.BhxhNldRate] = BhxhNldRate,
                    [PayrollFieldKeys.BhxhBhytCap] = Cap,
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
            return Cell(row, PayrollColumnCodes.NldBhxh);
        }

        // Dưới trần thì nền đã kẹp = chính mức lương ⇒ hai nhánh phải ra cùng một số.
        Run(small).Should().Be(Run(0m));
        Run(small).Should().Be(290_909m, "8.000.000 × 10/22 × 8% = 290.909,09 → làm tròn về đồng");
    }
}
