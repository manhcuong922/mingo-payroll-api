using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// Golden reconciliation: đối chiếu engine + bảng công thức mặc định với file lương mẫu THẬT
/// (08.1 F_SALARY_AUG.xlsm, sheet "4. LUONG", tháng 08/2023). Số liệu input lấy trực tiếp từ 2
/// dòng F5-0001 & F5-0003; kỳ vọng cột BD (Thực nhận) = 47.049.000 / 23.549.000 (đặc tả §8).
/// Dùng hằng số compliance + biểu thuế 2023 (đúng kỳ mẫu).
/// </summary>
public sealed class PayrollGoldenReconciliationTests
{
    private static decimal Cell(PayrollRow row, string code)
        => row.Cells.Single(c => c.ColumnCode == code).ComputedValue ?? 0m;

    // Biểu thuế lũy tiến 2023 (7 bậc) — đúng kỳ lương mẫu 08/2023.
    private static List<PitBracketSnapshot> Pit2023() => new()
    {
        new(1, 0m, 5_000_000m, 0.05m),
        new(2, 5_000_000m, 10_000_000m, 0.10m),
        new(3, 10_000_000m, 18_000_000m, 0.15m),
        new(4, 18_000_000m, 32_000_000m, 0.20m),
        new(5, 32_000_000m, 52_000_000m, 0.25m),
        new(6, 52_000_000m, 80_000_000m, 0.30m),
        new(7, 80_000_000m, null, 0.35m),
    };

    private static PayrollRow Compute(
        decimal mucLuong, decimal heSoNangSuat, decimal congChuan, decimal congChinhThuc,
        decimal congTinhCom, decimal dienThoai, decimal xangXe, decimal tangCa,
        decimal chenhLechLtg, decimal soNpt, decimal doanPhi, decimal phuCapCom)
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
                [PayrollFieldKeys.ContractMucLuong] = mucLuong,
                [PayrollFieldKeys.ProductivityCoefficient] = heSoNangSuat,
                [PayrollFieldKeys.CongChuan] = congChuan,
                [PayrollFieldKeys.CongChinhThuc] = congChinhThuc,
                [PayrollFieldKeys.CongThuViec] = 0m,
                [PayrollFieldKeys.CongHocViec] = 0m,
                [PayrollFieldKeys.CongTinhCom] = congTinhCom,
                // Cơm nay lấy từ phụ cấp MEAL của HĐ; file mẫu: 24 công × 40.000 = 960.000.
                [PayrollFieldKeys.ContractAllowanceCom] = phuCapCom,
                [PayrollFieldKeys.ContractAllowanceDienThoai] = dienThoai,
                [PayrollFieldKeys.ContractAllowanceXangXe] = xangXe,
                [PayrollFieldKeys.OtTangCa] = tangCa,
                [PayrollFieldKeys.OtChenhLech] = chenhLechLtg,
                [PayrollFieldKeys.DependentsCount] = soNpt,
                [PayrollFieldKeys.UnionFee] = doanPhi,
                // Compliance 08/2023
                [PayrollFieldKeys.BhxhDnRate] = 0.175m,
                [PayrollFieldKeys.BhxhNldRate] = 0.08m,
                [PayrollFieldKeys.BhytDnRate] = 0.03m,
                [PayrollFieldKeys.BhytNldRate] = 0.015m,
                [PayrollFieldKeys.BhtnDnRate] = 0.01m,
                [PayrollFieldKeys.BhtnNldRate] = 0.01m,
                [PayrollFieldKeys.KpcdRate] = 0.02m,
                [PayrollFieldKeys.BhxhBhytCap] = 29_800_000m,
                [PayrollFieldKeys.BhtnCap] = 93_600_000m,
                [PayrollFieldKeys.PersonalDeduction] = 11_000_000m,
                [PayrollFieldKeys.DependentDeduction] = 4_400_000m,
                [PayrollFieldKeys.MealExemptThreshold] = 730_000m,
            },
            Texts =
            {
                [PayrollFieldKeys.ContractInsuranceFlag] = InsuranceFlags.Full,
                [PayrollFieldKeys.ContractTaxFlag] = TaxFlags.Progressive,
            },
        };

        var ctx = new PayrollCalcContext
        {
            Fields = inputs.ToFields(catalog),
            FunctionContext = new PayrollFunctionContext { PitBrackets = Pit2023() },
        };

        var row = new PayrollRow();
        calculator.ComputeAll(plan, ctx, row);
        return row;
    }

    [Fact]
    public void Reconcile_F5_0001_KeToanTruong_MatchesSampleTakeHome()
    {
        // Input thật từ file mẫu (row F5-0001): O=14.641.000, hệ số P/O=2.2, công 23/23,
        // ĐT+xăng 1tr, cơm 24 công, tăng ca + chênh lệch LTG, 2 NPT, đoàn phí 2%×O.
        var row = Compute(
            mucLuong: 14_641_000m, heSoNangSuat: 2.2m, congChuan: 23m, congChinhThuc: 23m,
            congTinhCom: 24m, dienThoai: 500_000m, xangXe: 500_000m,
            tangCa: 4_074_017.392m, chenhLechLtg: 2_037_008.696m, soNpt: 2m, doanPhi: 292_820m,
            phuCapCom: 960_000m);

        Cell(row, PayrollColumnCodes.PhuCapLuong).Should().Be(32_210_200m);   // P
        Cell(row, PayrollColumnCodes.ThuNhap).Should().Be(48_811_200m);        // Y
        Cell(row, PayrollColumnCodes.CongBhNld).Should().Be(1_537_305m);       // AO
        Cell(row, PayrollColumnCodes.ThucNhan).Should().Be(47_049_000m);       // BD (golden)
    }

    [Fact]
    public void Reconcile_F5_0003_KySuCaoCap_MatchesSampleTakeHome()
    {
        // Input thật từ file mẫu (row F5-0003): O=7.700.000, hệ số 2.0, công 23/23,
        // ĐT+xăng 1tr, cơm 23 công, không tăng ca, 1 NPT, đoàn phí 2%×O.
        var row = Compute(
            mucLuong: 7_700_000m, heSoNangSuat: 2.0m, congChuan: 23m, congChinhThuc: 23m,
            congTinhCom: 23m, dienThoai: 500_000m, xangXe: 500_000m,
            tangCa: 0m, chenhLechLtg: 0m, soNpt: 1m, doanPhi: 154_000m,
            phuCapCom: 920_000m);

        Cell(row, PayrollColumnCodes.PhuCapLuong).Should().Be(15_400_000m);   // P
        Cell(row, PayrollColumnCodes.ThuNhap).Should().Be(25_020_000m);        // Y
        Cell(row, PayrollColumnCodes.CongBhNld).Should().Be(808_500m);         // AO
        Cell(row, PayrollColumnCodes.ThucNhan).Should().Be(23_549_000m);       // BD (golden)
    }
}
