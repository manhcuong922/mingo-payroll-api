using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// PRORATE-02 — phụ cấp khai NGOẠI LỆ (<c>AllowanceType.ExcludeFromProration</c>) được trả NGUYÊN
/// khoản, không chia theo ngày công; phụ cấp thường vẫn chia như <c>[D20]</c>.
///
/// <para>Quy ước đo: nhân viên làm <b>nửa công chuẩn</b> (11/22). Khoản chia theo công phải vào Thu
/// nhập đúng một nửa; khoản ngoại lệ phải vào đủ 100%. Mọi phép so sánh đối chiếu bằng CHÊNH LỆCH
/// giữa hai lần tính (có / không có khoản phụ cấp) nên không phụ thuộc phần lương nền.</para>
/// </summary>
public sealed class PayrollExceptionAllowanceTests
{
    private const decimal CongChuan = 22m;
    private const decimal CongThucTe = 11m;   // đúng nửa công chuẩn
    private const decimal MucLuong = 22_000_000m;
    private const decimal AllowanceAmount = 3_000_000m;

    private static decimal Cell(PayrollRow row, string code)
        => row.Cells.Single(c => c.ColumnCode == code).ComputedValue ?? 0m;

    /// <summary>Tính 1 dòng lương với bộ field cho sẵn (ghi đè lên nền tối thiểu).</summary>
    private static PayrollRow Compute(Action<Dictionary<string, decimal>> setup)
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
                [PayrollFieldKeys.ContractMucLuong] = MucLuong,
                [PayrollFieldKeys.ProductivityCoefficient] = 0m,   // bỏ phụ cấp lương chính cho gọn
                [PayrollFieldKeys.CongChuan] = CongChuan,
                [PayrollFieldKeys.CongChinhThuc] = CongThucTe,
                [PayrollFieldKeys.CongTinhCom] = CongThucTe,
                [PayrollFieldKeys.CongThuViec] = 0m,
                [PayrollFieldKeys.CongHocViec] = 0m,
                [PayrollFieldKeys.MealExemptThreshold] = 730_000m,
            },
            Texts =
            {
                [PayrollFieldKeys.ContractInsuranceFlag] = InsuranceFlags.Full,
                [PayrollFieldKeys.ContractTaxFlag] = TaxFlags.Progressive,
            },
        };
        setup(inputs.Numbers);

        var ctx = new PayrollCalcContext
        {
            Fields = inputs.ToFields(catalog),
            FunctionContext = new PayrollFunctionContext(),
        };

        var row = new PayrollRow();
        calculator.ComputeAll(plan, ctx, row);
        return row;
    }

    [Fact]
    public void ThuNhap_ProratesOrdinaryAllowance_ByActualWorkingDays()
    {
        // Phụ cấp thường (mã tự do → cột "Khác"), KHÔNG khai ngoại lệ.
        var baseline = Compute(_ => { });
        var withAllowance = Compute(n => n[PayrollFieldKeys.ContractAllowanceKhac] = AllowanceAmount);

        var delta = Cell(withAllowance, PayrollColumnCodes.ThuNhap) - Cell(baseline, PayrollColumnCodes.ThuNhap);

        delta.Should().Be(AllowanceAmount * CongThucTe / CongChuan,
            "phụ cấp thường phải chia theo công thực tế/công chuẩn ([D20] — nay áp cho cả mã tự do)");
    }

    [Fact]
    public void ThuNhap_PaysExceptionAllowance_InFull_RegardlessOfWorkingDays()
    {
        // Cùng số tiền, nhưng loại phụ cấp được khai NGOẠI LỆ ⇒ collector đẩy vào field ngoại lệ.
        var baseline = Compute(_ => { });
        var withException = Compute(n =>
        {
            n[PayrollFieldKeys.ContractAllowanceKhac] = AllowanceAmount;
            n[PayrollFieldKeys.ContractAllowanceNgoaiLe] = AllowanceAmount;
        });

        var delta = Cell(withException, PayrollColumnCodes.ThuNhap) - Cell(baseline, PayrollColumnCodes.ThuNhap);

        delta.Should().Be(AllowanceAmount,
            "khoản khai ngoại lệ phải vào Thu nhập NGUYÊN khoản dù chỉ làm nửa công chuẩn");
    }

    [Fact]
    public void ExceptionAllowance_KeepsFullAmountVisible_InItsOwnColumn()
    {
        // Cột gốc vẫn hiện đủ số ghi trong hợp đồng — kế toán đối chiếu được với HĐ, không bị
        // "mất tiền" sang cột khác chỉ vì khai ngoại lệ.
        var row = Compute(n =>
        {
            n[PayrollFieldKeys.ContractAllowanceThueNha] = AllowanceAmount;
            n[PayrollFieldKeys.ContractAllowanceNgoaiLe] = AllowanceAmount;
        });

        Cell(row, PayrollColumnCodes.ThueNha).Should().Be(AllowanceAmount);
    }

    [Fact]
    public void MealAllowance_DeclaredAsException_StaysInMealColumn_AndKeepsTaxExemption()
    {
        // Tiền cơm khai ngoại lệ vẫn phải nằm ở cột COM (không tách sang cột khác), nếu không thì
        // mất suất cơm miễn thuế MIN(COM, ngưỡng) — đúng lý do collector tách riêng field cho cơm.
        var normal = Compute(n => n[PayrollFieldKeys.ContractAllowanceCom] = AllowanceAmount);
        var exception = Compute(n => n[PayrollFieldKeys.ContractAllowanceComNgoaiLe] = AllowanceAmount);

        Cell(normal, PayrollColumnCodes.Com).Should().Be(AllowanceAmount * CongThucTe / CongChuan);
        Cell(exception, PayrollColumnCodes.Com).Should().Be(AllowanceAmount);

        // Suất miễn thuế cơm bám theo cột COM ⇒ khoản ngoại lệ vẫn được miễn tới ngưỡng.
        Cell(exception, PayrollColumnCodes.ComMienThue).Should().Be(730_000m);
    }

    [Fact]
    public void ExceptionFlag_DoesNotChangeAnything_WhenNoAllowanceIsFlagged()
    {
        // Hồi quy: mặc định (không ai khai ngoại lệ) phải ra ĐÚNG số như trước khi có cờ.
        var row = Compute(n => n[PayrollFieldKeys.ContractAllowanceThueNha] = AllowanceAmount);

        var baseline = Compute(_ => { });
        var delta = Cell(row, PayrollColumnCodes.ThuNhap) - Cell(baseline, PayrollColumnCodes.ThuNhap);

        delta.Should().Be(AllowanceAmount * CongThucTe / CongChuan);
    }
}
