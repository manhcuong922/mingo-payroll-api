using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// Phase 2 — bảng lương của HĐ KHOÁN VIỆC:
/// <list type="number">
///   <item>cột "Trợ cấp lương chính" (<c>PHUCAPLUONG</c>) = 0 — khoán không hưởng khoản theo hệ số
///     năng suất;</item>
///   <item>cột "Hỗ trợ theo tiến độ" (<c>HOTRO_TIENDO</c>) trả NGUYÊN số tiền của bậc giờ, KHÔNG bị
///     chia theo ngày công;</item>
///   <item>thuế = 10% tổng thu nhập, không có khoản bảo hiểm nào.</item>
/// </list>
///
/// <para>Điểm 2 là lý do khoản này nằm ở cột riêng thay vì ghi đè <c>PHUCAPLUONG</c>: cột đó nằm
/// trong nền chia theo ngày công của <c>THUNHAP_PHANBO</c>, nhét 200k vào đó thì người làm 20/26
/// công chỉ nhận ~154k — sai thoả thuận "đủ 150h là 200k".</para>
/// </summary>
public sealed class PieceWorkPayrollColumnsTests
{
    private static decimal Cell(PayrollRow row, string code)
        => row.Cells.Single(c => c.ColumnCode == code).ComputedValue ?? 0m;

    [Fact(DisplayName = "HĐ khoán: hỗ trợ trả nguyên, không BH, thuế 10%")]
    public void PieceWorkRow_PaysSupportInFull_NoInsurance_FlatTax()
    {
        var row = Compute(isPieceWork: true, support: 200_000m, congTinhCom: 20m, congChuan: 26m);

        Cell(row, PayrollColumnCodes.PhuCapLuong).Should().Be(0m,
            "HĐ khoán không hưởng trợ cấp lương chính theo hệ số năng suất");
        Cell(row, PayrollColumnCodes.HoTroTienDo).Should().Be(200_000m,
            "trả nguyên bậc giờ dù chỉ làm 20/26 công");

        Cell(row, PayrollColumnCodes.NldBhxh).Should().Be(0m);
        Cell(row, PayrollColumnCodes.NldBhyt).Should().Be(0m);
        Cell(row, PayrollColumnCodes.NldBhtn).Should().Be(0m);
        Cell(row, PayrollColumnCodes.Kpcd).Should().Be(0m);

        // Thu nhập = mức lương chia theo công (12tr/26×20 = 9.230.769) + hỗ trợ 200k.
        var expectedIncome = decimal.Round(12_000_000m / 26m * 20m, 0, MidpointRounding.AwayFromZero);
        var tongThuNhap = Cell(row, PayrollColumnCodes.TongThuNhap);
        tongThuNhap.Should().BeApproximately(expectedIncome + 200_000m, 1m);

        Cell(row, PayrollColumnCodes.ThueTncn).Should().BeApproximately(tongThuNhap * 0.10m, 1m,
            "cờ thuế 'x' ⇒ khoán 10% trên tổng thu nhập");
    }

    [Fact(DisplayName = "HĐLĐ: cột hỗ trợ = 0, trợ cấp lương chính giữ nguyên công thức cũ")]
    public void LaborRow_KeepsLegacyBehaviour()
    {
        var row = Compute(isPieceWork: false, support: 0m, congTinhCom: 26m, congChuan: 26m);

        Cell(row, PayrollColumnCodes.PhuCapLuong).Should().Be(12_000_000m,
            "mức lương × hệ số năng suất 1.0");
        Cell(row, PayrollColumnCodes.HoTroTienDo).Should().Be(0m);
    }

    [Fact(DisplayName = "Kỳ lương CŨ (snapshot thiếu field mới) → số không đổi")]
    public void LegacyPeriod_WithoutNewFields_IsUnchanged()
    {
        // Kỳ tạo trước Phase 2: ToFields điền 0 cho 3 khoá mới ⇒ isPieceWork = 0 ⇒ nhánh cũ.
        var legacy = Compute(isPieceWork: null, support: null, congTinhCom: 26m, congChuan: 26m);
        var current = Compute(isPieceWork: false, support: 0m, congTinhCom: 26m, congChuan: 26m);

        Cell(legacy, PayrollColumnCodes.PhuCapLuong).Should().Be(Cell(current, PayrollColumnCodes.PhuCapLuong));
        Cell(legacy, PayrollColumnCodes.ThucNhan).Should().Be(Cell(current, PayrollColumnCodes.ThucNhan));
    }

    /// <summary>
    /// Tính một dòng lương qua engine thật + bảng công thức mặc định.
    /// <paramref name="isPieceWork"/> / <paramref name="support"/> = null ⇒ KHÔNG điền khoá (mô phỏng
    /// snapshot của kỳ lương cũ).
    /// </summary>
    private static PayrollRow Compute(bool? isPieceWork, decimal? support, decimal congTinhCom, decimal congChuan)
    {
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
                [PayrollFieldKeys.CongChuan] = congChuan,
                [PayrollFieldKeys.CongTinhCom] = congTinhCom,
                [PayrollFieldKeys.CongChinhThuc] = congTinhCom,
                [PayrollFieldKeys.CongThuViec] = 0m,
                [PayrollFieldKeys.CongHocViec] = 0m,
                [PayrollFieldKeys.ContractMucLuong] = 12_000_000m,
                [PayrollFieldKeys.PersonalDeduction] = 15_500_000m,
                [PayrollFieldKeys.DependentDeduction] = 6_200_000m,
                [PayrollFieldKeys.BhxhNldRate] = 0.08m,
                [PayrollFieldKeys.BhytNldRate] = 0.015m,
                [PayrollFieldKeys.BhtnNldRate] = 0.01m,
                [PayrollFieldKeys.KpcdRate] = 0.02m,
                [PayrollFieldKeys.BhxhBhytCap] = 46_800_000m,
                [PayrollFieldKeys.BhtnCap] = 106_200_000m,
            },
            Texts =
            {
                [PayrollFieldKeys.ContractInsuranceFlag] =
                    isPieceWork == true ? InsuranceFlags.NoInsurance : InsuranceFlags.Full,
                [PayrollFieldKeys.ContractTaxFlag] =
                    isPieceWork == true ? TaxFlags.Khoan10 : TaxFlags.Progressive,
            },
        };

        if (isPieceWork is { } flag) inputs.Numbers[PayrollFieldKeys.ContractIsPieceWork] = flag ? 1m : 0m;
        if (support is { } amount) inputs.Numbers[PayrollFieldKeys.PieceWorkSupport] = amount;
        if (isPieceWork == true)
        {
            inputs.Numbers[PayrollFieldKeys.ContractNoSocialInsurance] = 1m;
            inputs.Numbers[PayrollFieldKeys.ContractNoHealthInsurance] = 1m;
            inputs.Numbers[PayrollFieldKeys.ContractNoUnemploymentInsurance] = 1m;
        }

        var row = new PayrollRow { RowType = PayrollRowType.Employee, EmployeeInformationId = 1 };
        calculator.ComputeAll(plan, new PayrollCalcContext
        {
            Fields = inputs.ToFields(catalog),
            FunctionContext = new PayrollFunctionContext { PitBrackets = PitBrackets() },
        }, row);

        return row;
    }

    private static IReadOnlyList<PitBracketSnapshot> PitBrackets() =>
    [
        new(1, 0m, 5_000_000m, 5m),
        new(2, 5_000_000m, 10_000_000m, 10m),
        new(3, 10_000_000m, 18_000_000m, 15m),
        new(4, 18_000_000m, 32_000_000m, 20m),
        new(5, 32_000_000m, 52_000_000m, 25m),
        new(6, 52_000_000m, 80_000_000m, 30m),
        new(7, 80_000_000m, null, 35m),
    ];
}
