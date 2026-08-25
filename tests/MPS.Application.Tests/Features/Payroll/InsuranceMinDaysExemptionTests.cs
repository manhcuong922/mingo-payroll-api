using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// BH-14 (QĐ 595 Đ.42 k.4–5; BHYT: NĐ 146/2018 Đ.7 k.8; BHTN: NĐ 28/2015 Đ.11) — tháng NLĐ có dưới
/// 14 ngày công hưởng lương thì KHÔNG đóng bảo hiểm tháng đó, chứ không phải đóng theo tỷ lệ ngày
/// công như engine vẫn làm.
///
/// <para>Test này đo <b>cổng cắt tiền</b> trên bảng công thức. Phép đếm ngày nằm ở collector
/// (<c>PayrollCollectorInsuranceMinDaysTests</c> bên MPS.Infrastructure.Tests).</para>
///
/// <para>Trục quan trọng nhất ở đây là <b>tương thích kỳ cũ</b>: cờ mang nghĩa "MIỄN" (1 = miễn) nên
/// kỳ lương chốt trước bản sửa — snapshot không có khoá, <c>ToFields</c> điền 0 — phải ra đúng số
/// tiền như trước. Nếu ai đó đảo cờ sang chiều thuận ("số ngày công"), mọi kỳ cũ sẽ đọc ra 0 ngày
/// ⇒ 0 &lt; 14 ⇒ xoá sạch bảo hiểm của toàn bộ lịch sử ngay lần tính lại đầu tiên.</para>
/// </summary>
public sealed class InsuranceMinDaysExemptionTests
{
    private const decimal Salary = 60_000_000m;
    private const decimal Cap = 46_800_000m;

    private static readonly string[] InsuranceColumns =
    {
        PayrollColumnCodes.DnBhxh, PayrollColumnCodes.NldBhxh,
        PayrollColumnCodes.DnBhyt, PayrollColumnCodes.NldBhyt,
        PayrollColumnCodes.DnBhtn, PayrollColumnCodes.NldBhtn,
        PayrollColumnCodes.Kpcd, PayrollColumnCodes.PhiCongDoanVien,
    };

    private static decimal Cell(PayrollRow row, string code) =>
        row.Cells.Single(c => c.ColumnCode == code).ComputedValue ?? 0m;

    /// <param name="mienBh">
    /// null = snapshot kỳ CŨ (không có khoá); 0 = đủ ngày công; 1 = dưới 14 ngày công.
    /// </param>
    private static PayrollRow Compute(decimal congLam, decimal? mienBh)
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
                [PayrollFieldKeys.CongChuan] = 22m,
                [PayrollFieldKeys.CongTinhCom] = congLam,
                [PayrollFieldKeys.CongChinhThuc] = congLam,
                [PayrollFieldKeys.CongHuongLuong] = congLam,
                [PayrollFieldKeys.ContractMucLuong] = Salary,
                [PayrollFieldKeys.ContractMucLuongCapBhxh] = Cap,
                [PayrollFieldKeys.BhxhNldRate] = 0.08m,
                [PayrollFieldKeys.BhxhDnRate] = 0.175m,
                [PayrollFieldKeys.BhytNldRate] = 0.015m,
                [PayrollFieldKeys.BhytDnRate] = 0.03m,
                [PayrollFieldKeys.BhtnNldRate] = 0.01m,
                [PayrollFieldKeys.BhtnDnRate] = 0.01m,
                [PayrollFieldKeys.KpcdRate] = 0.02m,
                [PayrollFieldKeys.UnionDuesRate] = 0.01m,
                [PayrollFieldKeys.UnionDuesCap] = 234_000m,
                [PayrollFieldKeys.UnionMember] = 1m,
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
        if (mienBh.HasValue)
            inputs.Numbers[PayrollFieldKeys.MienBhThieuNgayCong] = mienBh.Value;

        var ctx = new PayrollCalcContext
        {
            Fields = inputs.ToFields(new PayrollFieldCatalog()),
            FunctionContext = new PayrollFunctionContext { PitBrackets = Array.Empty<PitBracketSnapshot>() },
        };

        var row = new PayrollRow { RowType = PayrollRowType.Employee, EmployeeInformationId = 1 };
        calculator.ComputeAll(plan, ctx, row);
        return row;
    }

    [Fact(DisplayName = "BH14-F1 · Cờ miễn bật → cả 6 cột BH + KPCĐ + đoàn phí đều bằng 0")]
    public void MonthExempt_ZeroesTheWholeInsuranceBlock()
    {
        var row = Compute(congLam: 8m, mienBh: 1m);

        foreach (var code in InsuranceColumns)
            Cell(row, code).Should().Be(0m, "{0} phải bằng 0 khi tháng không thuộc diện đóng", code);

        Cell(row, PayrollColumnCodes.CongBhDn).Should().Be(0m);
        Cell(row, PayrollColumnCodes.CongBhNld).Should().Be(0m);
    }

    [Fact(DisplayName = "BH14-F2 · Cờ miễn tắt → thu bình thường (cổng không rò sang tháng đủ công)")]
    public void NotExempt_KeepsChargingInsurance()
    {
        var row = Compute(congLam: 20m, mienBh: 0m);

        foreach (var code in InsuranceColumns)
            Cell(row, code).Should().BeGreaterThan(0m, "{0} vẫn phải thu khi đủ ngày công", code);
    }

    [Fact(DisplayName = "BH14-F3 · Kỳ lương CŨ (snapshot không có khoá) giữ nguyên số đã chốt")]
    public void LegacyPeriod_WithoutTheKey_IsUnchanged()
    {
        // Cùng một dòng thiếu công: kỳ cũ (không có khoá) phải ra y hệt kỳ mới có cờ = 0.
        var legacy = Compute(congLam: 8m, mienBh: null);
        var explicitlyNotExempt = Compute(congLam: 8m, mienBh: 0m);

        foreach (var code in InsuranceColumns)
            Cell(legacy, code).Should().Be(Cell(explicitlyNotExempt, code),
                "{0} — cờ mang nghĩa MIỄN nên khoá vắng mặt ⇒ 0 ⇒ không miễn ai; đảo chiều cờ là " +
                "xoá sạch bảo hiểm của mọi kỳ đã chốt", code);

        Cell(legacy, PayrollColumnCodes.NldBhxh).Should().BeGreaterThan(0m,
            "kỳ cũ vẫn thu theo tỷ lệ ngày công như trước bản sửa");
    }

    [Fact(DisplayName = "BH14-F4 · Miễn BH thì THUẾ TNCN tăng — BH là khoản giảm trừ")]
    public void MonthExempt_RaisesTaxableIncome()
    {
        var exempt = Compute(congLam: 8m, mienBh: 1m);
        var charged = Compute(congLam: 8m, mienBh: 0m);

        Cell(exempt, PayrollColumnCodes.Tntt)
            .Should().BeGreaterThan(Cell(charged, PayrollColumnCodes.Tntt),
                "cắt bảo hiểm thì mất khoản giảm trừ ⇒ thu nhập tính thuế cao lên. Đúng luật, " +
                "nhưng kế toán sẽ thắc mắc nên ghim lại ở đây");
    }

    [Fact(DisplayName = "BH14-F5 · Ngưỡng đang hard-code là 14 ngày")]
    public void ThresholdIsFourteen()
    {
        LaborLawConstants.InsuranceEligibility.MinPaidWorkingDaysPerMonth.Should().Be(14,
            "đổi ngưỡng là đổi nghĩa vụ đóng BHXH của cả công ty — phải là thay đổi có chủ đích. " +
            "Khi chuyển ngưỡng sang cấu hình theo version luật, sửa test này cùng lúc");
    }
}
