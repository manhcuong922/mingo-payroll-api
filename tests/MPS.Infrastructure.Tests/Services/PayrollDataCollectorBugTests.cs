using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Domain.Enums.Payroll;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// ⚠️ <b>File tạm — các lỗi CÒN MỞ của báo cáo review 01/08/2026 chưa được sửa.</b>
///
/// <para>Mỗi test assert theo hành vi ĐÚNG nên hiện ĐỎ — đó là bằng chứng lỗi có thật. Khi một lỗi
/// được sửa, test tương ứng được chuyển sang file riêng theo chủ đề (xem
/// <see cref="PayrollCollectorOvertimeTests"/> cho A1, <see cref="DayTypeResolverRestDayTests"/>
/// cho A2) rồi commit cùng bản sửa, để mọi commit trên nhánh đều xanh. File này rỗng dần và được
/// xoá khi hết lỗi.</para>
///
/// <para>Còn lại: A4 (2 cột luôn bằng 0), A7 (cờ bảo hiểm chỉ map 3/8 tổ hợp),
/// C3 (CCCD làm mã NV), D3 (trần BHYT không có tác dụng).</para>
/// </summary>
public sealed class PayrollDataCollectorBugTests
{
    // ===================== A4 — hai cột vĩnh viễn bằng 0 =====================

    [Fact(DisplayName = "A4 · Thưởng dự án và Phí công đoàn viên phải được tra từ dữ liệu, không gán cứng 0")]
    public async Task A4_ProjectBonusAndUnionFee_MustNotBeHardcodedZero()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers.Should().ContainKey(PayrollFieldKeys.BonusThuongDuAn);
        inputs.Numbers.Should().ContainKey(PayrollFieldKeys.UnionFee);
        (inputs.Numbers[PayrollFieldKeys.BonusThuongDuAn] == 0m && inputs.Numbers[PayrollFieldKeys.UnionFee] == 0m)
            .Should().BeFalse(
                "cả hai cột luôn = 0 với mọi dữ liệu đầu vào ⇒ NLĐ không bao giờ nhận thưởng dự án " +
                "và công đoàn không bao giờ thu được phí; cần nguồn tra thật hoặc mở cho kế toán sửa tay");
    }

    // ===================== A7 (phần KPCĐ) — không có cách khai "miễn phí công đoàn" =====================
    //
    // Phần BẢO HIỂM của A7 đã sửa xong (xem PayrollContractInsuranceSwitchTests): mỗi cột BH nay
    // tôn trọng công tắc riêng của hợp đồng, đủ cả 8 tổ hợp. Phần KPCĐ dưới đây thì KHÔNG sửa được
    // bằng công thức — `Contract` không có trường "miễn phí công đoàn" nào cả, nên cờ `NoUnion`
    // (đã tồn tại trong enum và được công thức KPCĐ dùng) không nguồn nào sinh ra được.
    // Cần thêm cột vào `Contract` + migration + UI ⇒ vượt phạm vi một bản sửa lỗi, chờ PO chốt.

    [Fact(DisplayName = "A7 · Hợp đồng miễn phí công đoàn phải ra cờ NoUnion — hiện không tồn tại nguồn")]
    public async Task A7_NoUnionFlag_IsUnreachable()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        InsuranceFlags.All.Should().Contain(InsuranceFlags.NoUnion);
        inputs.Texts[PayrollFieldKeys.ContractInsuranceFlag].Should().Be(InsuranceFlags.NoUnion,
            "chưa có cách khai 'không đóng phí công đoàn' trên hợp đồng ⇒ hệ thống LUÔN thu KPCĐ");
    }

    // ===================== C3 — CCCD làm mã nhân viên =====================

    [Fact(DisplayName = "C3 · NV chưa có tài khoản: cột Mã NV không được lấy số CCCD")]
    public async Task C3_EmployeeCode_MustNotFallBackToCitizenId()
    {
        using var db = await SeedBaseAsync(employeeCode: null); // NV chưa được cấp tài khoản
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Texts[PayrollFieldKeys.EmployeeCode].Should().NotBe(CitizenId,
            "bảng lương và file Excel luân chuyển giữa kế toán/ngân hàng/BHXH ⇒ không được mang số CCCD");
    }

    // ===================== D3 — trần BHYT cấu hình được nhưng không có tác dụng =====================

    [Fact(DisplayName = "D3 · Đổi 'Hệ số trần BHYT' phải làm đổi số trên bảng lương")]
    public async Task D3_HealthCapMultiplier_MustActuallyAffectPayroll()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var withCap20 = await CollectAsync(db, new FakeComplianceLookup());
        // Admin sửa ô "Hệ số trần BHYT" 20 → 8 (BHXH giữ nguyên 20).
        var withCap8 = await CollectAsync(db, new FakeComplianceLookup { HealthCapMultiplierOverride = 8 });

        withCap8.Numbers[PayrollFieldKeys.BhxhBhytCap].Should().NotBe(
            withCap20.Numbers[PayrollFieldKeys.BhxhBhytCap],
            "màn hình cấu hình BHXH có ô 'Hệ số trần BHYT' (có validate hẳn hoi) nhưng payroll luôn " +
            "dùng SocialCapMultiplier cho cả BHYT ⇒ admin sửa xong không có gì thay đổi");
    }
}
