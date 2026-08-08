using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// ⭐ A4 (phần công đoàn) — CÔNG THỨC cột PHICONGDOANVIEN. Phía collector (cờ đoàn viên, tỷ lệ/trần
/// từ Compliance, cờ miễn KPCĐ) đo ở <c>MPS.Infrastructure.Tests.Services.PayrollUnionDuesTests</c>.
///
/// <para>Điểm dễ cài sai nhất: trần đoàn phí chặn trên <b>SỐ TIỀN đóng</b> (10% mức lương cơ sở,
/// QĐ 1908/QĐ-TLĐ Đ.23), khác toàn bộ khối BH vốn chặn trên <b>NỀN đóng</b>. Dùng nhầm
/// <c>BH_CO_TRAN</c> sẽ ra sai cả trăm lần — có test canh riêng bên dưới.</para>
/// </summary>
public sealed class PayrollUnionDuesFormulaTests
{
    /// <summary>Trần theo mức lương cơ sở 2.340.000 × 10%.</summary>
    private const decimal Cap = 234_000m;

    [Fact(DisplayName = "A4 · Đoàn phí = tỷ lệ × mức lương khi chưa chạm trần")]
    public void BelowCap_IsRateTimesSalary()
    {
        // 10.000.000 × 1% = 100.000 < trần 234.000 ⇒ lấy nguyên.
        UnionDues(salary: 10_000_000m, rate: 0.01m, cap: Cap, isMember: 1m).Should().Be(100_000m);
    }

    [Fact(DisplayName = "A4 · Chạm TRẦN SỐ TIỀN — trần chặn trên kết quả, không trên nền đóng")]
    public void AboveCap_IsClampedToCapAmount()
    {
        // 50.000.000 × 1% = 500.000 > trần 234.000 ⇒ còn đúng 234.000.
        // Nếu cài nhầm kiểu trần-trên-nền như khối BH (MIN(nền, trần) × tỷ lệ) sẽ ra
        // 234.000 × 1% = 2.340đ — sai 100 lần.
        UnionDues(salary: 50_000_000m, rate: 0.01m, cap: Cap, isMember: 1m).Should().Be(Cap);
    }

    [Fact(DisplayName = "A4 · Không phải đoàn viên ⇒ đoàn phí = 0 dù lương bao nhiêu")]
    public void NonMember_IsZero()
    {
        UnionDues(salary: 50_000_000m, rate: 0.01m, cap: Cap, isMember: 0m).Should().Be(0m);
    }

    [Fact(DisplayName = "A4 · Trần = 0 nghĩa là KHÔNG áp trần (quy ước chung với BH_CO_TRAN)")]
    public void ZeroCap_MeansNoCap()
    {
        // Chính sách trên file lương mẫu 08/2023: 2% mức lương, không trần.
        UnionDues(salary: 14_641_000m, rate: 0.02m, cap: 0m, isMember: 1m).Should().Be(292_820m);
    }

    [Fact(DisplayName = "A4 · Đoàn phí KHÔNG chia theo ngày công — là khoản đóng theo tháng")]
    public void IsNotProratedByAttendance()
    {
        // Công thực tế 13/26 mà vẫn phải ra đủ 1% lương: đoàn phí tính trên tiền lương làm căn cứ
        // đóng BHXH, không phải trên thu nhập thực tế của tháng.
        UnionDues(salary: 10_000_000m, rate: 0.01m, cap: Cap, isMember: 1m,
                congChuan: 26m, congTinhCom: 13m)
            .Should().Be(100_000m);
    }

    [Fact(DisplayName = "A4 · Kỳ lương cũ thiếu khoá mới ⇒ đoàn phí vẫn 0, không hồi tố thu tiền")]
    public void LegacyPeriodWithoutNewKeys_StaysZero()
    {
        // Snapshot input của kỳ tạo TRƯỚC bản sửa này không có 3 khoá đoàn phí; `ToFields` điền 0
        // ⇒ không đoàn viên + tỷ lệ 0 ⇒ 0, đúng bằng hành vi cũ (cột vốn hardcode 0).
        UnionDues(salary: 50_000_000m, rate: 0m, cap: 0m, isMember: 0m).Should().Be(0m);
    }

    /// <summary>
    /// Chạy ĐÚNG công thức của bảng mặc định (không chép lại phép tính vào test) trên bộ input tối
    /// thiểu đủ để cột PHICONGDOANVIEN tính được.
    /// </summary>
    private static decimal UnionDues(
        decimal salary, decimal rate, decimal cap, decimal isMember,
        decimal congChuan = 26m, decimal congTinhCom = 26m)
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
                [PayrollFieldKeys.ContractMucLuong] = salary,
                [PayrollFieldKeys.CongChuan] = congChuan,
                [PayrollFieldKeys.CongTinhCom] = congTinhCom,
                [PayrollFieldKeys.UnionDuesRate] = rate,
                [PayrollFieldKeys.UnionDuesCap] = cap,
                [PayrollFieldKeys.UnionMember] = isMember,
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
            FunctionContext = new PayrollFunctionContext(),
        };

        var row = new PayrollRow();
        calculator.ComputeAll(plan, ctx, row);

        return row.Cells.Single(c => c.ColumnCode == PayrollColumnCodes.PhiCongDoanVien).ComputedValue ?? 0m;
    }
}
