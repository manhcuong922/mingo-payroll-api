using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Domain.Enums.Payroll;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// ⭐ A4 (phần công đoàn) + A7 — phía COLLECTOR: hai khoản công đoàn tách bạch, mỗi khoản một nguồn
/// dữ liệu riêng. Công thức cột PHICONGDOANVIEN được đo riêng ở
/// <c>MPS.Application.Tests.Features.Payroll.PayrollUnionDuesFormulaTests</c>.
///
/// <para><b>ĐOÀN PHÍ</b> (cột PHICONGDOANVIEN) — NLĐ là đoàn viên đóng. Trước đây collector gán
/// cứng <c>0</c> và cột lại khai <c>Lookup</c> nên vừa không tự tính vừa không cho kế toán nhập
/// ⇒ chết cứng ở 0 với mọi dữ liệu. Nay: tỷ lệ + trần lấy từ Compliance
/// (<c>EmployeeUnionDuesPercent</c> / <c>EmployeeUnionDuesCapPercent</c>), nhân cờ
/// <c>EmployeeInformation.IsUnionMember</c>.</para>
///
/// <para><b>KPCĐ</b> (cột KPCD) — DN trích nộp cho mọi NLĐ, vốn đã tự tính đúng. Thiếu duy nhất
/// đường MIỄN: <c>ResolveInsuranceFlag</c> không nhánh nào trả <c>NoUnion</c> nên
/// <c>SkipIfFlag(FlagBh, noUnion)</c> trên công thức KPCĐ là code chết. Nay sinh từ
/// <c>Contract.UnionFundExemptFlag</c>.</para>
///
/// <para>Fixture dùng mức luật QĐ 1908/QĐ-TLĐ: 1% lương, trần 10% mức lương cơ sở
/// (2.340.000 × 10% = 234.000đ).</para>
/// </summary>
public sealed class PayrollUnionDuesTests
{
    /// <summary>Trần đoàn phí của fixture = ReferenceAmount 2.340.000 × 10%.</summary>
    private const decimal ExpectedCap = 234_000m;

    // ===================== Đoàn phí — cờ đoàn viên + tỷ lệ/trần =====================

    [Fact(DisplayName = "A4 · NV KHÔNG phải đoàn viên ⇒ cờ đoàn viên = 0 (không thu đoàn phí)")]
    public async Task NonMember_YieldsZeroFlag()
    {
        using var db = await SeedBaseAsync(isUnionMember: false);
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.UnionMember].Should().Be(0m);
    }

    [Fact(DisplayName = "A4 · NV là đoàn viên ⇒ cờ = 1, kèm tỷ lệ và trần từ Compliance")]
    public async Task Member_YieldsFlagAndComplianceRates()
    {
        using var db = await SeedBaseAsync(isUnionMember: true);
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.UnionMember].Should().Be(1m);
        inputs.Numbers[PayrollFieldKeys.UnionDuesRate].Should().Be(0.01m,
            "tỷ lệ phải lấy từ Compliance chứ không còn gán cứng");
        inputs.Numbers[PayrollFieldKeys.UnionDuesCap].Should().Be(ExpectedCap,
            "trần = mức lương cơ sở × EmployeeUnionDuesCapPercent");
    }

    // ===================== KPCĐ — cờ miễn =====================

    [Fact(DisplayName = "A7 · HĐ miễn KPCĐ ⇒ cờ BH = NoUnion")]
    public async Task UnionFundExemptContract_YieldsNoUnionFlag()
    {
        using var db = await SeedBaseAsync(unionFundExempt: true);
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Texts[PayrollFieldKeys.ContractInsuranceFlag].Should().Be(InsuranceFlags.NoUnion);
    }

    [Fact(DisplayName = "A7 · HĐ bình thường vẫn ra cờ Full — không miễn nhầm cho ai")]
    public async Task NormalContract_StaysFull()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Texts[PayrollFieldKeys.ContractInsuranceFlag].Should().Be(InsuranceFlags.Full);
    }

    [Fact(DisplayName = "A7 · HĐ vừa miễn BH vừa miễn KPCĐ ⇒ ưu tiên cờ BH (nặng hơn)")]
    public async Task NoInsuranceBeatsNoUnion()
    {
        // Cờ NoUnion nghĩa là "VẪN đóng BH nhưng không trích KPCĐ"; HĐ không đóng BH gì cả phải ra
        // NoInsurance, KPCĐ của trường hợp đó đã bị 3 công tắc BH cắt sẵn.
        using var db = await SeedBaseAsync(
            socialFlag: false, healthFlag: false, unemploymentFlag: false, unionFundExempt: true);
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Texts[PayrollFieldKeys.ContractInsuranceFlag].Should().Be(InsuranceFlags.NoInsurance);
    }
}
