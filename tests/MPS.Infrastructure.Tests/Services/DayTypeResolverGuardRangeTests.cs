using FluentAssertions;
using MPS.Domain.Entities.Attendance;
using MPS.Infrastructure.Services;
using AttendanceEnums = MPS.Domain.Enums.Attendance;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// Guard trước khi BatchUpdateShiftAssignments tạo ca — <see cref="DayTypeResolver.ResolveGuardRangeAsync"/>.
///
/// <para>Bug gốc: dùng <see cref="DayTypeResolver.ResolveRangeAsync"/> làm guard tạo vòng
/// luẩn quẩn. Khi user edit template mở rộng EffectiveTo từ tháng 8 sang tháng 9, batch
/// reapply gộp cả 8+9 vào 1 request → resolver dò assignments trên chính [08-01, 09-30]
/// → tháng 8 đã có Mon-Fri, tháng 9 rỗng → 22 weekday tháng 9 bị suy sai là WeeklyRestDay
/// → popup hiện dù không có ngày nghỉ/lễ thật.</para>
///
/// <para>⭐ RESTDAY-2026-08 — vòng luẩn quẩn đó sinh ra từ việc suy ngày nghỉ tuần từ roster.
/// Nay ngày nghỉ tuần đọc thẳng từ cấu hình (<c>AttendanceSetting.WeeklyRestDayMask</c>) nên guard
/// không còn phải dò lịch sử: khoảng add rỗng hay đầy đều cho cùng kết quả. Các test dưới đây giữ
/// nguyên để chốt rằng bug gốc KHÔNG tái phát.</para>
/// </summary>
public sealed class DayTypeResolverGuardRangeTests
{
    private static EmployeeShiftAssignment Assign(DateOnly date) => new()
    {
        EmployeeInformationId = EmpId,
        WorkDate = date,
        ShiftTemplateId = ShiftId,
        CreatedByUserId = UserId,
    };

    [Fact(DisplayName = "Guard · Extend template tháng 8→9: tháng 9 KHÔNG bị hit rest day khi lookback có Mon-Fri")]
    public async Task ExtendCrossMonth_LookbackHasWeekdayPattern_September_NotFlaggedRestDay()
    {
        using var db = NewContext();
        // Tháng 8/2026 đã có ca T2-T6 (khớp scenario "user tạo bảng tháng 8 xong").
        // T2 tháng 8/2026: 03, 10, 17, 24, 31 — T6: 07, 14, 21, 28. Seed vài ngày là đủ.
        foreach (var d in new[] { 3, 4, 5, 6, 7, 10, 11, 12, 13, 14, 17, 18, 19, 20, 21, 24, 25, 26, 27, 28, 31 })
            db.EmployeeShiftAssignments.Add(Assign(new DateOnly(2026, 8, d)));
        await db.SaveChangesAsync();

        var resolver = new DayTypeResolver(db);
        // Reapply gộp cả 8+9 → addFrom=08-01, addTo=09-30.
        var map = await resolver.ResolveGuardRangeAsync(
            EmpId, new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 30), CancellationToken.None);

        // Lookback = [07-04, 07-31] — rỗng → fallback T7/CN → weekday là Normal.
        // Đây chính là scenario user gặp: KHÔNG popup cho 22 weekday tháng 9.
        var septemberWeekdays = new[] { 1, 2, 3, 4, 7, 8, 9, 10, 11, 14, 15, 16, 17, 18, 21, 22, 23, 24, 25, 28, 29, 30 };
        foreach (var d in septemberWeekdays)
            map[new DateOnly(2026, 9, d)].Should().Be(AttendanceEnums.DayType.Normal,
                $"09/{d:D2}/2026 là weekday, lookback rỗng ⇒ fallback T7/CN ⇒ Normal");

        // Sanity: T7/CN tháng 9 vẫn là rest day (fallback default).
        map[new DateOnly(2026, 9, 5)].Should().Be(AttendanceEnums.DayType.WeeklyRestDay, "T7 05/09/2026");
        map[new DateOnly(2026, 9, 6)].Should().Be(AttendanceEnums.DayType.WeeklyRestDay, "CN 06/09/2026");
    }

    [Fact(DisplayName = "Guard · Extend tháng 9 sau khi có lịch sử Mon-Fri tháng 8: tháng 9 vẫn Normal weekday")]
    public async Task PaintSeptember_AfterAugustHistory_WeekdaysNormal()
    {
        using var db = NewContext();
        // Reapply DELTA — chỉ Add tháng 9 (min=09-01), lookback = [08-04, 08-31] có Mon-Fri.
        foreach (var d in new[] { 3, 4, 5, 6, 7, 10, 11, 12, 13, 14, 17, 18, 19, 20, 21, 24, 25, 26, 27, 28, 31 })
            db.EmployeeShiftAssignments.Add(Assign(new DateOnly(2026, 8, d)));
        await db.SaveChangesAsync();

        var resolver = new DayTypeResolver(db);
        var map = await resolver.ResolveGuardRangeAsync(
            EmpId, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), CancellationToken.None);

        // workingDows = {Mon,Tue,Wed,Thu,Fri} suy từ lookback.
        map[new DateOnly(2026, 9, 1)].Should().Be(AttendanceEnums.DayType.Normal, "T3");
        map[new DateOnly(2026, 9, 4)].Should().Be(AttendanceEnums.DayType.Normal, "T6");
        map[new DateOnly(2026, 9, 5)].Should().Be(AttendanceEnums.DayType.WeeklyRestDay, "T7 không có trong pattern");
        map[new DateOnly(2026, 9, 6)].Should().Be(AttendanceEnums.DayType.WeeklyRestDay, "CN không có trong pattern");
    }

    [Fact(DisplayName = "Guard · Paint tay T7 cho NV chỉ làm Mon-Fri ⇒ vẫn cảnh báo (không regress)")]
    public async Task PaintSaturday_ForMonFriEmployee_StillWarns()
    {
        using var db = NewContext();
        // Tháng 8 chỉ Mon-Fri.
        foreach (var d in new[] { 3, 4, 5, 6, 7, 10, 11, 12, 13, 14, 17, 18, 19, 20, 21, 24, 25, 26, 27, 28, 31 })
            db.EmployeeShiftAssignments.Add(Assign(new DateOnly(2026, 8, d)));
        await db.SaveChangesAsync();

        var resolver = new DayTypeResolver(db);
        // Paint tay 1 T7 tháng 9 lẻ.
        var saturday = new DateOnly(2026, 9, 5);
        var map = await resolver.ResolveGuardRangeAsync(EmpId, saturday, saturday, CancellationToken.None);

        map[saturday].Should().Be(AttendanceEnums.DayType.WeeklyRestDay,
            "workingDows từ lookback = {Mon..Fri}, T7 không thuộc ⇒ vẫn cảnh báo (yêu cầu của user)");
    }

    [Fact(DisplayName = "Guard · Công ty T2-T7 khai mask = chỉ CN ⇒ paint T7 KHÔNG cảnh báo")]
    public async Task PaintSaturday_ForMonSatCompany_DoesNotWarn()
    {
        using var db = NewContext();
        // ⭐ RESTDAY-2026-08 — xưởng T2–T7 nay khai bằng CẤU HÌNH, không để guard đoán qua roster:
        // roster đầy đủ T7 vẫn không biến T7 thành ngày thường (đó chính là lỗ hổng trả thiếu 100%).
        db.AttendanceSettings.Add(WeeklyRestSetting(DayOfWeek.Sunday));
        await db.SaveChangesAsync();

        var resolver = new DayTypeResolver(db);
        var saturday = new DateOnly(2026, 9, 5);
        var map = await resolver.ResolveGuardRangeAsync(EmpId, saturday, saturday, CancellationToken.None);

        map[saturday].Should().Be(AttendanceEnums.DayType.Normal,
            "nội quy ấn định chỉ nghỉ Chủ nhật ⇒ T7 là ngày làm việc bình thường (A2 semantics giữ nguyên)");
    }

    [Fact(DisplayName = "Guard · NV mới chưa có lịch sử ⇒ fallback T7/CN mặc định")]
    public async Task NoHistory_FallsBackToWeekendDefault()
    {
        using var db = NewContext();
        await db.SaveChangesAsync();

        var resolver = new DayTypeResolver(db);
        var map = await resolver.ResolveGuardRangeAsync(
            EmpId, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7), CancellationToken.None);

        map[new DateOnly(2026, 9, 1)].Should().Be(AttendanceEnums.DayType.Normal, "T3");
        map[new DateOnly(2026, 9, 5)].Should().Be(AttendanceEnums.DayType.WeeklyRestDay, "T7 fallback");
        map[new DateOnly(2026, 9, 6)].Should().Be(AttendanceEnums.DayType.WeeklyRestDay, "CN fallback");
        map[new DateOnly(2026, 9, 7)].Should().Be(AttendanceEnums.DayType.Normal, "T2");
    }

    [Fact(DisplayName = "Guard · PublicHoliday phủ trong khoảng add vẫn được nhận diện")]
    public async Task Holiday_WithinAddRange_MarkedAsHoliday()
    {
        using var db = NewContext();
        db.PublicHolidays.Add(new PublicHoliday
        {
            Name = "Quốc khánh",
            HolidayDate = new DateOnly(2026, 9, 2),
            HolidayEndDate = new DateOnly(2026, 9, 2),
            IsActive = true,
            CreatedByUserId = UserId,
        });
        // Seed lookback Mon-Fri để đảm bảo không rơi vào fallback.
        foreach (var d in new[] { 3, 4, 5, 6, 7, 10, 11, 12, 13, 14, 17, 18, 19, 20, 21, 24, 25, 26, 27, 28, 31 })
            db.EmployeeShiftAssignments.Add(Assign(new DateOnly(2026, 8, d)));
        await db.SaveChangesAsync();

        var resolver = new DayTypeResolver(db);
        var map = await resolver.ResolveGuardRangeAsync(
            EmpId, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), CancellationToken.None);

        map[new DateOnly(2026, 9, 2)].Should().Be(AttendanceEnums.DayType.PublicHoliday,
            "lễ trong khoảng add vẫn phải trigger holiday guard");
        map[new DateOnly(2026, 9, 1)].Should().Be(AttendanceEnums.DayType.Normal);
        map[new DateOnly(2026, 9, 3)].Should().Be(AttendanceEnums.DayType.Normal);
    }
}
