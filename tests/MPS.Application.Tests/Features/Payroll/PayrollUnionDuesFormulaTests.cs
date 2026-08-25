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

    // ===== X3 — hai tầng chặn còn thiếu =====

    /// <summary>
    /// Cờ BH nói KHÔNG thì đoàn phí phải = 0: <c>x</c> là "không đóng phí công đoàn" nói thẳng ra
    /// điều đó, <c>x1</c>/<c>TS</c> là không có tiền lương làm căn cứ đóng BHXH nên không có căn cứ
    /// thu. Trước bản sửa cột này không nhìn cờ, cứ là đoàn viên là thu.
    /// </summary>
    /// <summary>
    /// ⚠️ Vòng sửa X3 đầu tiên KHOÁ NHẦM ở đây: <c>x2</c> từng nằm ở nhóm "vẫn thu" bên dưới, vì tôi
    /// đọc nó là "còn đóng bảo hiểm". Sai — <c>x2</c> là CHỈ đóng BHYT, tức KHÔNG đóng BHXH, mà đoàn
    /// phí thu trên tiền lương làm căn cứ đóng BHXH. Bản sửa X7 chuyển nó sang đúng nhóm này.
    /// </summary>
    [Theory(DisplayName = "X3/X7 · Cờ BH cắt căn cứ đóng BHXH ⇒ đoàn phí = 0")]
    [InlineData(InsuranceFlags.NoUnion)]
    [InlineData(InsuranceFlags.NoInsurance)]
    [InlineData(InsuranceFlags.Maternity)]
    [InlineData(InsuranceFlags.HealthOnly)]
    public void InsuranceFlagThatStopsContributions_StopsUnionDues(string flag)
    {
        UnionDues(salary: 7_000_000m, rate: 0.01m, cap: Cap, isMember: 1m, flag: flag)
            .Should().Be(0m, $"cờ '{flag}' nói tháng này không có căn cứ đóng BHXH");
    }

    /// <summary>Đối chứng: cờ đóng đầy đủ thì vẫn thu như cũ.</summary>
    [Fact(DisplayName = "X3 · Đối chứng — cờ đóng đầy đủ thì đoàn phí vẫn thu như cũ")]
    public void FullFlag_KeepsUnionDues()
    {
        UnionDues(salary: 7_000_000m, rate: 0.01m, cap: Cap, isMember: 1m, flag: InsuranceFlags.Full)
            .Should().Be(70_000m);
    }

    /// <summary>
    /// Nghỉ thai sản / nghỉ không lương TRỌN THÁNG: cờ vẫn là <c>x3</c> nên tầng cờ không cứu được,
    /// nhưng không có ngày công nào thuộc diện đóng ⇒ không có tiền lương làm căn cứ ⇒ không thu.
    ///
    /// <para>Đây là ca đẻ ra hậu quả dây chuyền đo được trên DB: thu nhập 0 → vẫn trừ đoàn phí →
    /// thực nhận âm → sàn kẹp về 0 và GHI NỢ treo sang kỳ sau.</para>
    /// </summary>
    [Fact(DisplayName = "X3 · Không có ngày công nào thuộc diện đóng ⇒ đoàn phí = 0")]
    public void NoInsuredWorkingDay_ChargesNothing()
    {
        UnionDues(salary: 6_500_000m, rate: 0.01m, cap: Cap, isMember: 1m,
                congChuan: 22m, congTinhCom: 0m)
            .Should().Be(0m, "nghỉ trọn tháng thì tháng đó không có căn cứ đóng");
    }

    [Fact(DisplayName = "X3 · Mọi ngày công đều thuộc HĐ không đóng BH ⇒ đoàn phí = 0")]
    public void AllDaysOutsideInsurance_ChargesNothing()
    {
        UnionDues(salary: 6_500_000m, rate: 0.01m, cap: Cap, isMember: 1m,
                congChuan: 22m, congTinhCom: 22m, congKhongBaoHiem: 22m)
            .Should().Be(0m, "HĐ thử việc riêng không đóng BH nào thì cũng không có căn cứ thu đoàn phí");
    }

    /// <summary>
    /// Tầng chặn thứ hai là NHỊ PHÂN, không phải chia theo công: có ngày công đóng BH thì thu ĐỦ
    /// đoàn phí tháng. Cài nhầm thành prorate sẽ làm hỏng bài đối chiếu file lương thật 08/2023.
    /// </summary>
    [Fact(DisplayName = "X3 · Chỉ cần MỘT ngày công đóng BH là thu đủ đoàn phí tháng")]
    public void OneInsuredDay_StillChargesTheFullMonth()
    {
        UnionDues(salary: 7_000_000m, rate: 0.01m, cap: Cap, isMember: 1m,
                congChuan: 22m, congTinhCom: 1m)
            .Should().Be(70_000m, "đoàn phí là khoản đóng theo THÁNG, chặn nhị phân chứ không chia");
    }

    // ===== X7 — bám theo CĂN CỨ ĐÓNG BHXH, không chỉ bám cờ =====

    /// <summary>
    /// Đoàn phí thu trên "tiền lương làm căn cứ đóng BHXH" (QĐ 1908 Đ.23). Hợp đồng TẮT RIÊNG BHXH
    /// mà vẫn bật BHYT + BHTN thì không có căn cứ đó ⇒ không thu.
    ///
    /// <para>⚠️ Cờ KHÔNG cứu được ca này: <c>ResolveInsuranceFlag</c> chỉ trả <c>x2</c> khi BHYT bật
    /// mà BHXH VÀ BHTN đều tắt; tắt mỗi BHXH thì không khớp nhánh nào và rơi xuống <c>x3</c> —
    /// "đóng đầy đủ". Cờ nói CÓ, thực tế BHXH = 0. Chỉ công tắc hợp đồng mới bắt được.</para>
    /// </summary>
    [Fact(DisplayName = "X7 · HĐ tắt riêng BHXH (cờ vẫn x3) ⇒ đoàn phí = 0")]
    public void ContractWithSocialInsuranceOff_ChargesNoUnionDues()
    {
        UnionDues(salary: 7_000_000m, rate: 0.01m, cap: Cap, isMember: 1m,
                flag: InsuranceFlags.Full, noSocialInsurance: 1m)
            .Should().Be(0m, "không có quỹ tiền lương làm căn cứ đóng BHXH thì không có gì để nhân tỷ lệ");
    }

    /// <summary>
    /// Cờ <c>x2</c> = chỉ đóng BHYT, tức KHÔNG đóng BHXH. Tập cờ cắt đoàn phí trước đây thiếu đúng
    /// cờ này nên người mang nó vẫn bị thu.
    /// </summary>
    [Fact(DisplayName = "X7 · Cờ x2 (chỉ đóng BHYT) ⇒ đoàn phí = 0")]
    public void HealthOnlyFlag_ChargesNoUnionDues()
    {
        UnionDues(salary: 7_000_000m, rate: 0.01m, cap: Cap, isMember: 1m, flag: InsuranceFlags.HealthOnly)
            .Should().Be(0m, "cờ x2 nói rõ không đóng BHXH");
    }

    [Fact(DisplayName = "X7 · Đối chứng — HĐ đóng đủ BHXH thì vẫn thu như cũ")]
    public void ContractWithSocialInsuranceOn_StillCharges()
    {
        UnionDues(salary: 7_000_000m, rate: 0.01m, cap: Cap, isMember: 1m,
                flag: InsuranceFlags.Full, noSocialInsurance: 0m)
            .Should().Be(70_000m);
    }

    /// <summary>
    /// Chạy ĐÚNG công thức của bảng mặc định (không chép lại phép tính vào test) trên bộ input tối
    /// thiểu đủ để cột PHICONGDOANVIEN tính được.
    /// </summary>
    private static decimal UnionDues(
        decimal salary, decimal rate, decimal cap, decimal isMember,
        decimal congChuan = 26m, decimal congTinhCom = 26m,
        string flag = InsuranceFlags.Full, decimal congKhongBaoHiem = 0m,
        decimal noSocialInsurance = 0m)
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
                [PayrollFieldKeys.CongKhongBaoHiem] = congKhongBaoHiem,
                [PayrollFieldKeys.ContractNoSocialInsurance] = noSocialInsurance,
            },
            Texts =
            {
                [PayrollFieldKeys.ContractInsuranceFlag] = flag,
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
