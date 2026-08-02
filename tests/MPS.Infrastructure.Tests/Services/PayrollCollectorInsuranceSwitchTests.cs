using FluentAssertions;
using MPS.Application.Common.Payroll;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// A7 (phần nối dữ liệu) — ba công tắc bảo hiểm của hợp đồng phải chảy đúng vào bảng lương.
///
/// <para><see cref="PayrollContractInsuranceSwitchTests"/> (tầng Application) đã chứng minh công
/// thức tôn trọng đủ 8 tổ hợp. File này chốt nốt nửa còn lại: <c>PayrollDataCollector</c> đọc đúng
/// <c>Contract.SocialInsuranceFlag</c> / <c>HealthInsuranceFlag</c> / <c>UnemploymentInsuranceFlag</c>
/// và ghi ra ba trường "MIỄN" (1 = miễn).</para>
///
/// <para>Ngữ nghĩa "miễn" thay vì "có đóng" là cố ý — xem ghi chú ở <c>PayrollFieldKeys</c>: khoá
/// thiếu ⇒ 0 ⇒ không miễn, nên kỳ lương tạo trước bản sửa giữ nguyên số cũ.</para>
/// </summary>
public sealed class PayrollCollectorInsuranceSwitchTests
{
    [Theory(DisplayName = "A7 · Công tắc BH của hợp đồng chảy đúng vào bảng lương (1 = miễn)")]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]   // tổ hợp của báo cáo: có BHXH, không BHYT, không BHTN
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public async Task ContractSwitches_FlowIntoPayrollInputs(bool social, bool health, bool unemployment)
    {
        using var db = await SeedBaseAsync(social, health, unemployment);
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractNoSocialInsurance].Should().Be(social ? 0m : 1m,
            "HĐ {0} đóng BHXH", social ? "CÓ" : "KHÔNG");
        inputs.Numbers[PayrollFieldKeys.ContractNoHealthInsurance].Should().Be(health ? 0m : 1m,
            "HĐ {0} đóng BHYT", health ? "CÓ" : "KHÔNG");
        inputs.Numbers[PayrollFieldKeys.ContractNoUnemploymentInsurance].Should().Be(unemployment ? 0m : 1m,
            "HĐ {0} đóng BHTN", unemployment ? "CÓ" : "KHÔNG");
    }

    [Fact(DisplayName = "A7 · Không có hợp đồng hiệu lực ⇒ mặc định đóng đủ (không miễn gì)")]
    public async Task WithoutActiveContract_DefaultsToPayingEverything()
    {
        using var db = await SeedBaseAsync();
        db.Contracts.RemoveRange(db.Contracts);
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractNoSocialInsurance].Should().Be(0m);
        inputs.Numbers[PayrollFieldKeys.ContractNoHealthInsurance].Should().Be(0m);
        inputs.Numbers[PayrollFieldKeys.ContractNoUnemploymentInsurance].Should().Be(0m,
            "thiếu hợp đồng thì không được tự ý miễn bảo hiểm cho ai");
    }
}
