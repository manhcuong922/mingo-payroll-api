using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// Phase 7 — cột THUETNCN nhánh <b>khoán 10%</b> (TT 111/2013 Đ.25.1.i): tỷ lệ + ngưỡng khấu trừ lấy
/// từ Compliance thay vì hằng số trong công thức.
///
/// <para>Ngưỡng "mỗi lần chi trả" nâng 2.000.000đ → <b>5.000.000đ</b> từ 01/07/2026 (NĐ 253/2026/NĐ-CP):
/// tổng thu nhập DƯỚI ngưỡng thì KHÔNG khấu trừ đồng nào. Bảng lương chi 1 lần/tháng nên "mỗi lần chi
/// trả" = tổng thu nhập của kỳ.</para>
///
/// <para>Hai bẫy được canh riêng bên dưới:</para>
/// <list type="bullet">
///   <item>kỳ lương CŨ không có 2 khoá mới ⇒ <c>ToFields</c> điền 0 ⇒ tỷ lệ 0 sẽ <b>xoá sạch thuế
///     khoán</b> nếu không có nhánh fallback về 0.10;</item>
///   <item>ngưỡng chỉ áp cho khấu trừ 10% của cá nhân CƯ TRÚ; mức 20% của cá nhân không cư trú
///     (Đ.25.1.a) không có ngưỡng nào.</item>
/// </list>
/// </summary>
public sealed class FlatRateTaxThresholdFormulaTests
{
    /// <summary>Ngưỡng khấu trừ từ 01/07/2026 — NĐ 253/2026/NĐ-CP.</summary>
    private const decimal Threshold = 5_000_000m;

    [Fact(DisplayName = "Phase 7 · Dưới ngưỡng 5tr ⇒ KHÔNG khấu trừ")]
    public void BelowThreshold_NoWithholding()
    {
        Tax(income: 4_900_000m, rate: 0.10m, threshold: Threshold).Should().Be(0m,
            "TT 111/2013 Đ.25.1.i + NĐ 253/2026 — dưới 5.000.000đ/lần chi trả thì không khấu trừ");
    }

    [Fact(DisplayName = "Phase 7 · ĐÚNG ngưỡng 5tr ⇒ vẫn khấu trừ (biên: 'từ 5tr trở lên')")]
    public void AtThreshold_IsWithheld()
    {
        // Biên dễ cài lệch một nấc: điều khoản viết "từ 2.000.000 trở lên" (nay 5.000.000), tức
        // ĐÚNG ngưỡng vẫn phải khấu trừ. Cài `<=` sẽ tha đúng dòng 5tr chẵn.
        Tax(income: 5_000_000m, rate: 0.10m, threshold: Threshold).Should().Be(500_000m);
    }

    [Fact(DisplayName = "Phase 7 · Trên ngưỡng ⇒ 10% TOÀN BỘ tổng thu nhập, không trừ phần dưới ngưỡng")]
    public void AboveThreshold_TaxesWholeIncome()
    {
        // Ngưỡng là điều kiện CÓ/KHÔNG khấu trừ, không phải mức giảm trừ: 12tr ⇒ 1,2tr chứ không
        // phải (12tr − 5tr) × 10% = 700k.
        Tax(income: 12_000_000m, rate: 0.10m, threshold: Threshold).Should().Be(1_200_000m);
    }

    [Fact(DisplayName = "Phase 7 · Tỷ lệ lấy từ Compliance, không phải hằng số 10% trong công thức")]
    public void Rate_ComesFromCompliance()
    {
        // Đổi tỷ lệ trên màn hình PitRegulation là số thuế phải đổi theo — trước Phase 7 tỷ lệ
        // hardcode `0.10` nên 2 ô ShortTermFlatRatePercent / ShortTermTaxExemptMonthlyIncome có CRUD
        // đầy đủ mà không đường nào đọc.
        Tax(income: 10_000_000m, rate: 0.05m, threshold: Threshold).Should().Be(500_000m);
    }

    [Fact(DisplayName = "Phase 7 · Kỳ lương CŨ (thiếu 2 khoá) ⇒ vẫn 10%, không ngưỡng")]
    public void LegacyPeriod_KeepsFlatTenPercentWithoutThreshold()
    {
        // Snapshot input của kỳ tạo TRƯỚC Phase 7 không có 2 khoá ⇒ ToFields điền 0.
        //  • tỷ lệ 0 ⇒ PHẢI fallback 0.10, nếu không kỳ cũ tính lại ra thuế = 0 (thất thu, sai số cũ);
        //  • ngưỡng 0 ⇒ mọi thu nhập đều >= 0 ⇒ khấu trừ như trước.
        Tax(income: 4_900_000m, rate: null, threshold: null).Should().Be(490_000m);
    }

    [Fact(DisplayName = "Phase 7 · Khoán 20% (cá nhân KHÔNG cư trú) không có ngưỡng")]
    public void NonResidentTwentyPercent_HasNoThreshold()
    {
        // Đ.25.1.a: cá nhân không cư trú khấu trừ 20% trên thu nhập, không kèm ngưỡng miễn.
        Tax(income: 4_900_000m, rate: 0.10m, threshold: Threshold, taxFlag: TaxFlags.Khoan20)
            .Should().Be(980_000m);
    }

    [Fact(DisplayName = "Phase 7 · Thuế lũy tiến không bị ngưỡng khoán chạm vào")]
    public void ProgressiveTax_IsUntouched()
    {
        // Ngưỡng khoán không được rò sang HĐLĐ: 30tr thu nhập vẫn tính theo biểu luỹ tiến.
        Tax(income: 30_000_000m, rate: 0.10m, threshold: Threshold, taxFlag: TaxFlags.Progressive)
            .Should().BeGreaterThan(0m);
    }

    /// <summary>
    /// Chạy ĐÚNG công thức của bảng mặc định trên bộ input tối thiểu sao cho
    /// <c>TONGTHUNHAP</c> = <paramref name="income"/>: hệ số năng suất để trống (⇒ trợ cấp lương
    /// chính = 0) và công thực tế = công chuẩn (⇒ thu nhập không bị chia).
    ///
    /// <para><paramref name="rate"/> / <paramref name="threshold"/> = null ⇒ KHÔNG điền khoá, mô
    /// phỏng snapshot của kỳ lương cũ.</para>
    /// </summary>
    private static decimal Tax(
        decimal income, decimal? rate, decimal? threshold, string taxFlag = TaxFlags.Khoan10)
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
                [PayrollFieldKeys.ContractMucLuong] = income,
                [PayrollFieldKeys.CongChuan] = 26m,
                [PayrollFieldKeys.CongTinhCom] = 26m,
                [PayrollFieldKeys.CongChinhThuc] = 26m,
                [PayrollFieldKeys.PersonalDeduction] = 15_500_000m,
                [PayrollFieldKeys.DependentDeduction] = 6_200_000m,
            },
            Texts =
            {
                [PayrollFieldKeys.ContractInsuranceFlag] = InsuranceFlags.NoInsurance,
                [PayrollFieldKeys.ContractTaxFlag] = taxFlag,
            },
        };

        if (rate is { } r) inputs.Numbers[PayrollFieldKeys.ShortTermFlatRate] = r;
        if (threshold is { } t) inputs.Numbers[PayrollFieldKeys.ShortTermTaxExemptIncome] = t;

        var row = new PayrollRow { RowType = PayrollRowType.Employee, EmployeeInformationId = 1 };
        calculator.ComputeAll(plan, new PayrollCalcContext
        {
            Fields = inputs.ToFields(catalog),
            FunctionContext = new PayrollFunctionContext { PitBrackets = PitBrackets() },
        }, row);

        // Chốt giả định của helper: mọi case đo thuế trên đúng con số income truyền vào.
        row.Cells.Single(c => c.ColumnCode == PayrollColumnCodes.TongThuNhap).ComputedValue
            .Should().Be(income, "helper phải dựng được tổng thu nhập đúng bằng số truyền vào");

        return row.Cells.Single(c => c.ColumnCode == PayrollColumnCodes.ThueTncn).ComputedValue ?? 0m;
    }

    private static IReadOnlyList<PitBracketSnapshot> PitBrackets() =>
    [
        new(1, 0m, 10_000_000m, 5m),
        new(2, 10_000_000m, 30_000_000m, 10m),
        new(3, 30_000_000m, 60_000_000m, 20m),
        new(4, 60_000_000m, 100_000_000m, 30m),
        new(5, 100_000_000m, null, 35m),
    ];
}
