using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// Phase 5 — bảng lương của kỳ có CẢ ngày thử việc lẫn ngày chính thức.
///
/// <para>Trước Phase 5, <c>TROCAPTHUVIEC = PHUCAPLUONG</c> nên hai nhánh thử việc / chính thức của
/// <c>THU_NHAP_PHANBO</c> BẰNG NHAU — tách công ra cũng không tạo chênh lệch tiền nào, tức nửa
/// tháng còn thử việc vẫn được trả theo mức chính thức.</para>
///
/// <para>Nay collector tính sẵn <c>contract.troCapThuViec</c> và cột chỉ tra khoá đó
/// (KHÔNG hằng số 0.85 trong công thức — xem <c>PayrollFieldKeys.ContractTroCapThuViec</c>).</para>
/// </summary>
public sealed class ProbationSegmentPayrollColumnsTests
{
    private const decimal OfficialSalary = 10_000_000m;

    private static decimal Cell(PayrollRow row, string code)
        => row.Cells.Single(c => c.ColumnCode == code).ComputedValue ?? 0m;

    [Fact(DisplayName = "Nửa kỳ thử việc được trả theo MỨC THỬ VIỆC, nửa kỳ chính thức theo mức chính thức")]
    public void MixedPeriod_PaysEachSegmentAtItsOwnRate()
    {
        // Mức thử việc 8,5tr; hệ số năng suất 1.0 ⇒ trợ cấp thử việc = 8,5 × 2 − 10 = 7tr.
        var row = Compute(congThuViec: 13m, congChinhThuc: 13m, troCapThuViec: 7_000_000m);

        // Chính thức: (10tr + 10tr)/26 × 13 = 10tr — mức chính thức × (1 + hệ số), nửa tháng.
        // Thử việc:   (10tr +  7tr)/26 × 13 = 8,5tr — đúng bằng mức thử việc 8,5tr × (1 + hệ số) / 2.
        Cell(row, PayrollColumnCodes.ThuNhap).Should().BeApproximately(18_500_000m, 1m);

        // Công thức CŨ (TROCAPTHUVIEC = PHUCAPLUONG = 10tr) cho ra 20tr — trả dư 1,5tr cho nửa
        // tháng còn thử việc. Chốt luôn con số cũ để lần sau ai sửa lại là test đỏ.
        Compute(congThuViec: 13m, congChinhThuc: 13m, troCapThuViec: OfficialSalary)
            .Cells.Single(c => c.ColumnCode == PayrollColumnCodes.ThuNhap).ComputedValue
            .Should().BeApproximately(20_000_000m, 1m);
    }

    [Fact(DisplayName = "Kỳ không có ngày thử việc → trợ cấp thử việc = 0, thu nhập không đổi")]
    public void NoProbationDays_IsUnaffected()
    {
        var row = Compute(congThuViec: 0m, congChinhThuc: 26m, troCapThuViec: 0m);

        // (10tr + 10tr)/26 × 26 = 20tr — y hệt trước Phase 5.
        Cell(row, PayrollColumnCodes.ThuNhap).Should().BeApproximately(20_000_000m, 1m);
        Cell(row, PayrollColumnCodes.TroCapThuViec).Should().Be(0m);
    }

    [Fact(DisplayName = "Nền đóng BH TRỪ công của những ngày không đóng BH")]
    public void InsuranceBase_ExcludesUninsuredDays()
    {
        // 13/26 công thuộc HĐ thử việc (không BH) ⇒ nền đóng = 10tr/26 × 13 = 5tr.
        var row = Compute(congThuViec: 13m, congChinhThuc: 13m, troCapThuViec: 7_000_000m,
            congKhongBaoHiem: 13m);

        Cell(row, PayrollColumnCodes.NldBhxh).Should().BeApproximately(400_000m, 1m,
            "8% × 5.000.000 — không được thu trên cả tháng khi nửa đầu chưa thuộc diện đóng");
    }

    [Fact(DisplayName = "Kỳ lương CŨ (thiếu khoá công không BH) → nền đóng vẫn là toàn bộ công")]
    public void LegacyPeriod_KeepsFullInsuranceBase()
    {
        // Snapshot input của kỳ tạo trước Phase 5 không có khoá ⇒ ToFields điền 0 ⇒ trừ 0.
        var row = Compute(congThuViec: 0m, congChinhThuc: 26m, troCapThuViec: 0m,
            congKhongBaoHiem: null);

        Cell(row, PayrollColumnCodes.NldBhxh).Should().BeApproximately(800_000m, 1m,
            "8% × 10.000.000 — đúng bằng hành vi trước Phase 5");
    }

    /// <summary>
    /// Một dòng lương chạy qua engine thật + bảng công thức mặc định.
    /// <paramref name="congKhongBaoHiem"/> = null ⇒ KHÔNG điền khoá (mô phỏng kỳ lương cũ).
    /// </summary>
    private static PayrollRow Compute(
        decimal congThuViec, decimal congChinhThuc, decimal troCapThuViec,
        decimal? congKhongBaoHiem = 0m)
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
                [PayrollFieldKeys.ContractMucLuong] = OfficialSalary,
                [PayrollFieldKeys.ContractTroCapThuViec] = troCapThuViec,
                [PayrollFieldKeys.ProductivityCoefficient] = 1.0m,
                [PayrollFieldKeys.CongChuan] = 26m,
                [PayrollFieldKeys.CongTinhCom] = congThuViec + congChinhThuc,
                [PayrollFieldKeys.CongThuViec] = congThuViec,
                [PayrollFieldKeys.CongChinhThuc] = congChinhThuc,
                [PayrollFieldKeys.CongHocViec] = 0m,
                [PayrollFieldKeys.BhxhNldRate] = 0.08m,
                [PayrollFieldKeys.BhxhBhytCap] = 46_800_000m,
                [PayrollFieldKeys.BhtnCap] = 106_200_000m,
            },
            Texts =
            {
                [PayrollFieldKeys.ContractInsuranceFlag] = InsuranceFlags.Full,
                [PayrollFieldKeys.ContractTaxFlag] = TaxFlags.Progressive,
            },
        };

        if (congKhongBaoHiem is { } cong) inputs.Numbers[PayrollFieldKeys.CongKhongBaoHiem] = cong;

        var row = new PayrollRow { RowType = PayrollRowType.Employee, EmployeeInformationId = 1 };
        calculator.ComputeAll(plan, new PayrollCalcContext
        {
            Fields = inputs.ToFields(catalog),
            FunctionContext = new PayrollFunctionContext(),
        }, row);

        return row;
    }
}
