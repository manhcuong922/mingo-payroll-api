using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Domain.Entities.Attendance;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// HOLIDAY-400-2026-08 — đi làm ngày lễ phải được <b>400%</b>, không phải 300%.
///
/// <para>Đ.112.1 cho nghỉ lễ mà vẫn hưởng nguyên lương (100%, engine trả qua <c>CONGLE</c> kể cả
/// khi NLĐ ở nhà). Đ.98.1c đòi thêm <b>ít nhất 300%</b> cho giờ làm vào ngày đó, "chưa kể tiền
/// lương ngày nghỉ lễ". Trước bản này phụ trội chỉ trả phần chênh 200% nên tổng ra 300%: phần
/// thưởng cho việc hy sinh ngày nghỉ lễ chỉ còn +200%, thiếu đúng một ngày lương.</para>
///
/// <para>Kỳ 07/2026, lương 10tr, công chuẩn 26 ⇒ đơn giá giờ ≈ 48.076,92đ. Ngày 21/07 là Thứ Ba.</para>
/// </summary>
public sealed class PayrollCollectorHolidayPayTests
{
    private const int Tuesday = 21;

    private static PublicHoliday HolidayOn(int day) => new()
    {
        Name = "[E2E] Lễ thử",
        HolidayDate = D(day),
        HolidayEndDate = D(day),
        IsActive = true,
        CreatedByUserId = UserId,
    };

    [Fact(DisplayName = "LỄ · Đi làm trọn ca ngày lễ ⇒ phụ trội NGUYÊN 300% (tổng 400%)")]
    public async Task WorkingAFullHoliday_EarnsThreeHundredPercentOnTop()
    {
        using var db = await SeedBaseAsync();
        db.PublicHolidays.Add(HolidayOn(Tuesday));
        db.AttendanceRecords.Add(ShiftRecordAt(Tuesday, new TimeOnly(8, 0), new TimeOnly(17, 0)));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongChinhThuc].Should().Be(1.00m,
            "ngày lễ đi làm vẫn chỉ là MỘT công — 300% kia đi qua cột phụ trội, không độn công");
        inputs.Numbers[PayrollFieldKeys.PremiumDayType].Should().BeApproximately(
            8m * HourlyRate * 3.0m, 1m,
            "Đ.98.1c: 300% cho giờ làm, chưa kể tiền lương ngày nghỉ lễ đã trả qua cột Thu nhập");
    }

    [Fact(DisplayName = "LỄ · Nghỉ lễ ở nhà ⇒ vẫn 1 công hưởng lương, không có phụ trội")]
    public async Task StayingHomeOnAHoliday_StillEarnsTheDay()
    {
        using var db = await SeedBaseAsync();
        db.PublicHolidays.Add(HolidayOn(Tuesday));
        // Không có bản ghi chấm công nào — NLĐ nghỉ theo chế độ.
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongLe].Should().Be(1.00m, "Đ.112.1 — nghỉ lễ hưởng nguyên lương");
        inputs.Numbers[PayrollFieldKeys.PremiumDayType].Should().Be(0m, "không đi làm thì không có phụ trội");
    }

    [Fact(DisplayName = "LỄ · Chênh lệch giữa đi làm và ở nhà đúng bằng 300% một ngày công")]
    public async Task MarginalRewardForWorkingAHoliday_IsThreeHundredPercent()
    {
        using var atHome = await SeedBaseAsync();
        atHome.PublicHolidays.Add(HolidayOn(Tuesday));
        await atHome.SaveChangesAsync();

        using var atWork = await SeedBaseAsync();
        atWork.PublicHolidays.Add(HolidayOn(Tuesday));
        atWork.AttendanceRecords.Add(ShiftRecordAt(Tuesday, new TimeOnly(8, 0), new TimeOnly(17, 0)));
        await atWork.SaveChangesAsync();

        var home = await CollectAsync(atHome);
        var work = await CollectAsync(atWork);

        // Hai bên có cùng số công hưởng lương (1,0) — bên ở nhà qua CONGLE, bên đi làm qua chấm công.
        home.Numbers[PayrollFieldKeys.CongHuongLuong].Should().Be(work.Numbers[PayrollFieldKeys.CongHuongLuong]);

        var marginal = work.Numbers[PayrollFieldKeys.PremiumDayType] - home.Numbers[PayrollFieldKeys.PremiumDayType];
        marginal.Should().BeApproximately(8m * HourlyRate * 3.0m, 1m,
            "hy sinh ngày nghỉ lễ phải được thưởng đúng 300% — bản cũ chỉ cho +200%");
    }

    [Fact(DisplayName = "LỄ · Ngày nghỉ hằng tuần KHÔNG đổi: vẫn chỉ bù phần chênh tới 200%")]
    public async Task WeeklyRestDay_KeepsTopUpToTwoHundredPercent()
    {
        using var db = await SeedBaseAsync();
        // 11/07/2026 là Thứ Bảy ⇒ ngày nghỉ hằng tuần theo mặc định T7 + CN.
        db.AttendanceRecords.Add(ShiftRecordAt(11, new TimeOnly(8, 0), new TimeOnly(17, 0)));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.PremiumDayType].Should().BeApproximately(
            8m * HourlyRate * 1.0m, 1m,
            "T7/CN không đi làm thì KHÔNG có đồng nào, nên 100% đã trả chính là tiền công của việc " +
            "đi làm hôm đó ⇒ chỉ bù phần chênh tới 200% (Đ.98.1b)");
    }
}
