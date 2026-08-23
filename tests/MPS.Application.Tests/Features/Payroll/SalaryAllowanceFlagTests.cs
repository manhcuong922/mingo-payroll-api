using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// X2 — hai cờ <c>IsBhxhBase</c> / <c>IsTaxable</c> của danh mục phụ cấp phải ĐI TỚI bảng lương.
///
/// <para><b>Lỗi gốc.</b> <c>AllowanceBuckets.From</c> phân loại phụ cấp chỉ theo 4 MÃ CỨNG
/// (HOUSING/PHONE/FUEL/MEAL); mọi mã khác dồn vào cột "Khác" và bị đối xử như nhau. Hai cờ trên
/// <c>AllowanceType</c> chưa từng được đọc ngoài CRUD, nên kế toán tick xong không có tác dụng gì.
/// Ba hậu quả đo được trên DB seed 3 kỳ 05–07/2026:</para>
/// <list type="bullet">
///   <item><b>X2a</b> — thợ có phụ cấp độc hại 500.000 (đã tick "vào nền BHXH"): nền đóng ra
///   6.300.000 thay vì 6.800.000 ⇒ đóng THIẾU 40.000đ/tháng.</item>
///   <item><b>X2b</b> — tổ trưởng có phụ cấp trách nhiệm 800.000: đơn giá giờ chỉ lấy lương cơ bản
///   7.400.000 thay vì 8.200.000 ⇒ tiền tăng ca trả THIẾU tới 105.000đ/tháng.</item>
///   <item><b>X2c</b> — công tác phí khoán 2.000.000 và trang phục 400.000 (đã tick "không chịu
///   thuế") vẫn bị tính đủ vào thu nhập chịu thuế ⇒ thuế thu THỪA.</item>
/// </list>
/// </summary>
public sealed class SalaryAllowanceFlagTests
{
    private const decimal Salary = 6_300_000m;
    private const decimal Toxic = 500_000m;
    private const decimal BhxhNldRate = 0.08m;

    private static decimal Cell(PayrollRow row, string code) =>
        row.Cells.Single(c => c.ColumnCode == code).ComputedValue ?? 0m;

    /// <param name="nenPhuCapLuong">Tổng phụ cấp mang cờ "vào nền BHXH". 0 = snapshot kỳ CŨ.</param>
    /// <param name="mienThue">Tổng phụ cấp mang cờ "không chịu thuế". 0 = snapshot kỳ CŨ.</param>
    private static PayrollRow Compute(decimal nenPhuCapLuong, decimal mienThue, decimal khac)
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
                [PayrollFieldKeys.CongHuongLuong] = 23m,
                [PayrollFieldKeys.ContractMucLuong] = Salary,
                [PayrollFieldKeys.ContractAllowanceKhac] = khac,
                [PayrollFieldKeys.ContractAllowanceNenPhuCapLuong] = nenPhuCapLuong,
                [PayrollFieldKeys.ContractAllowanceMienThue] = mienThue,
                [PayrollFieldKeys.BhxhNldRate] = BhxhNldRate,
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
            FunctionContext = new PayrollFunctionContext { PitBrackets = Array.Empty<PitBracketSnapshot>() },
        };

        var row = new PayrollRow { RowType = PayrollRowType.Employee, EmployeeInformationId = 1 };
        calculator.ComputeAll(plan, ctx, row);
        return row;
    }

    // ===== Tầng gom phụ cấp =====

    [Fact(DisplayName = "X2 — phụ cấp tick 'vào nền BHXH' được gom riêng, không lẫn với cột Khác")]
    public void BhxhBaseFlag_IsAccumulatedSeparately()
    {
        var buckets = AllowanceBuckets.From(new[]
        {
            new AllowanceLine("TOXIC", ExcludeFromProration: false, Toxic, IsBhxhBase: true, IsTaxable: true),
            new AllowanceLine("UNIFORM", ExcludeFromProration: false, 400_000m, IsBhxhBase: false, IsTaxable: false),
        });

        buckets.NenPhuCapLuong.Should().Be(Toxic, "chỉ mã tick 'vào nền BHXH' mới được cộng");
        buckets.Khac.Should().Be(Toxic + 400_000m, "hai mã đều ngoài 4 cột cứng nên vẫn hiện ở cột Khác");
    }

    [Fact(DisplayName = "X2c — cơm KHÔNG vào tổng miễn thuế vì đã có ngưỡng riêng ở cột ANTRUA")]
    public void MealIsExcludedFromTaxExemptTotal_ToAvoidDoubleDeduction()
    {
        var buckets = AllowanceBuckets.From(new[]
        {
            new AllowanceLine("MEAL", ExcludeFromProration: false, 1_000_000m, IsBhxhBase: false, IsTaxable: false),
            new AllowanceLine("LUNCH", ExcludeFromProration: false, 730_000m, IsBhxhBase: false, IsTaxable: false),
            new AllowanceLine("PERDIEM", ExcludeFromProration: true, 2_000_000m, IsBhxhBase: false, IsTaxable: false),
        });

        buckets.TongMienThue.Should().Be(2_000_000m, "chỉ công tác phí; cơm đi đường ANTRUA có ngưỡng");
        buckets.Com.Should().Be(1_000_000m);
    }

    [Fact(DisplayName = "X2 — cộng dồn / chia bình quân phải mang theo cả hai tổng mới")]
    public void DerivedTotals_SurviveAccumulationAndAveraging()
    {
        var half = AllowanceBuckets.From(new[]
        {
            new AllowanceLine("RESP", ExcludeFromProration: false, 800_000m, IsBhxhBase: true, IsTaxable: true),
            new AllowanceLine("PERDIEM", ExcludeFromProration: true, 2_000_000m, IsBhxhBase: false, IsTaxable: false),
        });

        // Phụ lục đổi phụ cấp giữa tháng: 10 ngày mức này + 10 ngày mức này, bình quân 20 công.
        var averaged = (half.Scale(10m) + half.Scale(10m)).Divide(20m);

        averaged.NenPhuCapLuong.Should().Be(800_000m, "bỏ sót ở operator+ / Divide là phụ cấp lương biến mất đúng tháng có phụ lục");
        averaged.TongMienThue.Should().Be(2_000_000m);
    }

    // ===== X2a — nền đóng bảo hiểm =====

    [Fact(DisplayName = "X2a — phụ cấp lương vào nền đóng BHXH")]
    public void SalaryAllowance_EntersInsuranceBase()
    {
        var row = Compute(nenPhuCapLuong: Toxic, mienThue: 0m, khac: Toxic);

        Cell(row, PayrollColumnCodes.NldBhxh)
            .Should().Be((Salary + Toxic) * BhxhNldRate, "QĐ 595 Đ.30.2 — nền là tiền lương CỘNG phụ cấp lương");
    }

    [Fact(DisplayName = "X2a — snapshot kỳ CŨ thiếu khoá ⇒ nền đóng giữ nguyên số cũ")]
    public void LegacySnapshot_KeepsOldInsuranceBase()
    {
        var row = Compute(nenPhuCapLuong: 0m, mienThue: 0m, khac: Toxic);

        Cell(row, PayrollColumnCodes.NldBhxh)
            .Should().Be(Salary * BhxhNldRate, "kỳ tạo trước bản sửa không được đổi số bảo hiểm");
    }

    // ===== X2c — thu nhập chịu thuế =====

    [Fact(DisplayName = "X2c — phụ cấp tick 'không chịu thuế' bị trừ khỏi thu nhập chịu thuế")]
    public void TaxExemptAllowance_IsDeductedFromTaxableIncome()
    {
        var withFlag = Compute(nenPhuCapLuong: 0m, mienThue: 400_000m, khac: 400_000m);
        var withoutFlag = Compute(nenPhuCapLuong: 0m, mienThue: 0m, khac: 400_000m);

        (Cell(withoutFlag, PayrollColumnCodes.Tnct) - Cell(withFlag, PayrollColumnCodes.Tnct))
            .Should().Be(400_000m, "trang phục / công tác phí khoán là khoản luật cho miễn");
    }

    [Fact(DisplayName = "X2c — snapshot kỳ CŨ thiếu khoá ⇒ thu nhập chịu thuế giữ nguyên")]
    public void LegacySnapshot_KeepsOldTaxableIncome()
    {
        var row = Compute(nenPhuCapLuong: 0m, mienThue: 0m, khac: 400_000m);

        Cell(row, PayrollColumnCodes.Tnct)
            .Should().Be(Cell(row, PayrollColumnCodes.TongThuNhap) - Cell(row, PayrollColumnCodes.ComMienThue),
                "không có khoá miễn thuế mới ⇒ chỉ trừ đúng những khoản cũ");
    }
}
