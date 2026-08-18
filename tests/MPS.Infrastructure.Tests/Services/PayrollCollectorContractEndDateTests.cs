using FluentAssertions;
using MPS.Application.Common.Payroll;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// Phase 4 — biên NGÀY của khoảng hiệu lực hợp đồng.
///
/// <para><b>Vì sao cần.</b> `EffectiveFrom`/`EffectiveTo` là <c>DateTime</c> nhưng nghiệp vụ chỉ
/// quan tâm tới NGÀY: `EffectiveTo` là ngày làm việc CUỐI CÙNG (inclusive). Mọi nơi ghi hai mốc này
/// đều để phần giờ = 00:00 (form HĐ, `TerminationDate`), trong khi collector so ngày công tại mốc
/// 12:00 trưa ⇒ <c>EffectiveTo (00:00) >= ngày công (12:00)</c> là FALSE và <b>ngày làm việc cuối
/// cùng bị mất công</b>.</para>
///
/// <para>Trước Phase 4 lỗi này ẩn vì `EffectiveTo` gần như luôn null (HĐ vô thời hạn) hoặc rơi vào
/// cuối tháng. Từ khi chấm dứt HĐ tự đóng `EffectiveTo = TerminationDate`, nó rơi vào giữa kỳ và
/// mất công của đúng ngày cuối cùng NLĐ đi làm.</para>
/// </summary>
public sealed class PayrollCollectorContractEndDateTests
{
    [Fact(DisplayName = "EffectiveTo = ngày làm việc cuối (00:00) → ngày đó VẪN được tính công")]
    public async Task LastWorkingDay_IsStillCounted()
    {
        using var db = await SeedBaseAsync();
        var contract = db.Contracts.Single();
        contract.EffectiveTo = new DateTime(Year, Month, 10);   // 10/07/2026 00:00
        db.AttendanceRecords.Add(ShiftRecord(day: 10));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongTinhCom].Should().Be(1m,
            "EffectiveTo là ngày làm việc CUỐI CÙNG (inclusive) — mất công ngày này là trả thiếu 1 ngày lương");
    }

    [Fact(DisplayName = "Ngày công SAU EffectiveTo không được tính")]
    public async Task DayAfterEffectiveTo_IsNotCounted()
    {
        using var db = await SeedBaseAsync();
        var contract = db.Contracts.Single();
        contract.EffectiveTo = new DateTime(Year, Month, 10);
        db.AttendanceRecords.Add(ShiftRecord(day: 11));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongTinhCom].Should().Be(0m,
            "NLĐ đã nghỉ từ 11/07 — chấm công lỡ nhập sau ngày đó không được ra tiền");
    }

    [Fact(DisplayName = "Ngày công ĐÚNG EffectiveFrom (00:00) được tính")]
    public async Task FirstWorkingDay_IsCounted()
    {
        using var db = await SeedBaseAsync();
        var contract = db.Contracts.Single();
        contract.EffectiveFrom = new DateTime(Year, Month, 15);
        db.AttendanceRecords.Add(ShiftRecord(day: 15));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongTinhCom].Should().Be(1m,
            "ngày vào làm đầu tiên phải có công — biên đối xứng với EffectiveTo");
    }

    [Fact(DisplayName = "EffectiveTo có phần giờ cuối ngày vẫn tính đúng ngày đó")]
    public async Task EffectiveTo_WithEndOfDayTime_StillCountsThatDay()
    {
        // Dữ liệu cũ có thể lưu EffectiveTo kèm giờ (23:59:59) — so sánh theo NGÀY phải bỏ qua phần giờ.
        using var db = await SeedBaseAsync();
        var contract = db.Contracts.Single();
        contract.EffectiveTo = new DateTime(Year, Month, 10, 23, 59, 59);
        db.AttendanceRecords.Add(ShiftRecord(day: 10));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongTinhCom].Should().Be(1m);
    }
}
