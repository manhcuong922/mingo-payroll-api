using FluentAssertions;
using MPS.Application.Common.Payroll;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// D3 — trần đóng BHYT cấu hình được nhưng không có tác dụng.
/// C3 — số CCCD bị dùng làm mã nhân viên trên bảng lương.
///
/// <para>Hai lỗi cùng nằm ở bước gom dữ liệu của <c>PayrollDataCollector</c>.</para>
/// </summary>
public sealed class PayrollCollectorCapAndCodeTests
{
    // ===================== D3 =====================

    [Fact(DisplayName = "D3 · Trần BHYT phải tính theo hệ số RIÊNG của nó, không dùng chung trần BHXH")]
    public async Task HealthCap_UsesItsOwnMultiplier()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var withCap20 = await CollectAsync(db, new FakeComplianceLookup());
        // Admin sửa ô "Hệ số trần BHYT" 20 → 8 (hệ số trần BHXH giữ nguyên 20).
        var withCap8 = await CollectAsync(db, new FakeComplianceLookup { HealthCapMultiplierOverride = 8 });

        withCap8.Numbers[PayrollFieldKeys.BhytCap].Should().NotBe(
            withCap20.Numbers[PayrollFieldKeys.BhytCap],
            "màn hình cấu hình BHXH có ô 'Hệ số trần BHYT' (có validate hẳn hoi) nhưng payroll luôn " +
            "dùng SocialCapMultiplier cho cả BHYT ⇒ admin sửa xong không có gì thay đổi");

        withCap8.Numbers[PayrollFieldKeys.BhxhBhytCap].Should().Be(
            withCap20.Numbers[PayrollFieldKeys.BhxhBhytCap],
            "đổi hệ số trần BHYT KHÔNG được kéo theo trần BHXH — hai trần độc lập");
    }

    [Fact(DisplayName = "D3 · Trần BHYT = mức tham chiếu × hệ số trần BHYT")]
    public async Task HealthCap_EqualsReferenceTimesItsMultiplier()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db, new FakeComplianceLookup { HealthCapMultiplierOverride = 8 });

        // FakeComplianceLookup: ReferenceAmount = 2.340.000; SocialCapMultiplier = 20.
        inputs.Numbers[PayrollFieldKeys.BhytCap].Should().Be(2_340_000m * 8m);
        inputs.Numbers[PayrollFieldKeys.BhxhBhytCap].Should().Be(2_340_000m * 20m);
    }

    // ===================== C3 =====================

    [Fact(DisplayName = "C3 · NV chưa có tài khoản: cột Mã NV để trống, KHÔNG lấy số CCCD")]
    public async Task EmployeeCode_NeverFallsBackToCitizenId()
    {
        using var db = await SeedBaseAsync(employeeCode: null); // NV chưa được cấp tài khoản
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Texts[PayrollFieldKeys.EmployeeCode].Should().NotBe(CitizenId,
            "bảng lương và file Excel luân chuyển giữa kế toán/ngân hàng/BHXH ⇒ không được mang số CCCD");
        inputs.Texts[PayrollFieldKeys.EmployeeCode].Should().BeEmpty(
            "ô trống nói đúng sự thật 'chưa được cấp mã'; readiness đã có cổng cảnh báo hồ sơ thiếu");
    }

    [Fact(DisplayName = "C3 · Đối chứng — NV đã có tài khoản vẫn hiện đúng mã nhân viên")]
    public async Task EmployeeCode_IsShownWhenAccountExists()
    {
        using var db = await SeedBaseAsync(employeeCode: "NV001");
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Texts[PayrollFieldKeys.EmployeeCode].Should().Be("NV001",
            "test đối chứng: bản sửa C3 không được làm mất mã của người ĐÃ có tài khoản");
    }
}
