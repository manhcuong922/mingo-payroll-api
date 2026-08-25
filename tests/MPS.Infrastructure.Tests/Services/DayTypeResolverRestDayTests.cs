using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Infrastructure.Services;
using AttendanceEnums = MPS.Domain.Enums.Attendance;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// RESTDAY-2026-08 — ngày nghỉ hằng tuần đọc từ CẤU HÌNH (<c>AttendanceSetting.WeeklyRestDayMask</c>),
/// không suy từ bảng phân ca.
///
/// <para>Điều 111 BLLĐ để doanh nghiệp ẤN ĐỊNH ngày nghỉ hằng tuần trong nội quy lao động — đó là
/// một tuyên bố, không phải thứ đoán được từ việc hôm đó có ai xếp ca hay chưa. Bản A2 cũ suy từ
/// roster nên sai cả hai chiều: xếp một ca Thứ Bảy cho công ty T2–T6 thì mất 100% phụ trội, còn
/// roster xếp thiếu ngày thì những ngày trống lại ăn phụ trội oan.</para>
///
/// <para>Ý định của A2 (xưởng làm T2–T7 thì Thứ Bảy là ngày thường) vẫn giữ nguyên — nay khai bằng
/// cấu hình mask = chỉ Chủ nhật thay vì đoán qua roster.</para>
///
/// <para>Kỳ dùng chung 07/2026 — các Thứ Bảy: 04, 11, 18, 25.</para>
/// </summary>
public sealed class DayTypeResolverRestDayTests
{
    private static readonly int[] Saturdays = { 4, 11, 18, 25 };
    private const int Sunday = 12;
    private const int Wednesday = 8;

    // ===================== Tác động lên bảng lương =====================

    [Fact(DisplayName = "RESTDAY · Thứ Bảy có xếp ca vẫn là ngày nghỉ tuần ⇒ được trả phụ trội 200% (Đ.98.1b)")]
    public async Task ScheduledSaturday_StaysAWeeklyRestDay()
    {
        using var db = await SeedBaseAsync();
        // Công ty T2–T6 nhưng HR xếp ca cả 4 Thứ Bảy (trực / làm bù) và NV đi làm đủ.
        foreach (var day in Saturdays)
        {
            db.EmployeeShiftAssignments.Add(ShiftAssignment(day));
            db.AttendanceRecords.Add(ShiftRecord(day));
        }
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.WeeklyRestHours].Should().Be(32m,
            "4 Thứ Bảy × 8h — xếp ca KHÔNG làm mất tư cách ngày nghỉ hằng tuần đã ấn định trong nội quy");
        inputs.Numbers[PayrollFieldKeys.PremiumDayType].Should().BeApproximately(
            32m * HourlyRate * 1.0m, 1m,
            "Đ.98.1b đòi ít nhất 200%; cột Thu nhập đã trả 100% ⇒ phụ trội phải bù đúng 100% còn lại");
        inputs.Numbers[PayrollFieldKeys.CongTinhCom].Should().Be(4m, "vẫn phải được tính đủ 4 công");
    }

    [Fact(DisplayName = "RESTDAY · OT vào Thứ Bảy đã xếp ca hưởng 200%, không tụt về 150%")]
    public async Task OvertimeOnScheduledSaturday_UsesWeeklyRestMultiplier()
    {
        using var db = await SeedBaseAsync();
        db.EmployeeShiftAssignments.Add(ShiftAssignment(11)); // 11/07/2026 — Thứ Bảy có lịch
        db.OvertimeRequests.Add(ApprovedOt(1, day: 11, new TimeOnly(18, 0), new TimeOnly(22, 0)));
        // OT chỉ ra tiền khi có bản ghi chấm công đối chứng (xem PayrollCollectorFixture.OtRecord).
        db.AttendanceRecords.Add(OtRecord(day: 11, otRequestId: 1, new TimeOnly(18, 0), new TimeOnly(22, 0)));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.OtTangCa].Should().BeApproximately(HourlyRate * 4m * 2.0m, 1m,
            "Thứ Bảy là ngày nghỉ hằng tuần theo nội quy ⇒ OT 200% (Đ.98.1b), " +
            "việc HR có xếp ca hôm đó hay không không đổi được điều đó");
    }

    [Fact(DisplayName = "RESTDAY · Ngày T2–T6 KHÔNG có trong lịch phân ca vẫn là ngày làm việc bình thường")]
    public async Task UnscheduledWeekday_DoesNotEarnRestDayPremium()
    {
        using var db = await SeedBaseAsync();
        // Roster mới xếp tới ngày 07; ngày 08 (Thứ Tư) chưa xếp nhưng NV vẫn được chấm công —
        // chấm công KHÔNG đòi phải có bản phân ca (xem RecordAttendanceCommandHandler).
        foreach (var day in new[] { 6, 7 })
            db.EmployeeShiftAssignments.Add(ShiftAssignment(day));
        db.AttendanceRecords.Add(ShiftRecord(Wednesday));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.WeeklyRestHours].Should().Be(0m,
            "roster xếp thiếu ngày là lỗi dữ liệu, không phải tuyên bố ngày nghỉ hằng tuần");
        inputs.Numbers[PayrollFieldKeys.PremiumDayType].Should().Be(0m,
            "bản A2 cũ trả phụ trội 100% oan cho mọi ngày roster chưa phủ");
    }

    // ===================== Hành vi của resolver =====================

    private static async Task<AttendanceEnums.DayType> ResolveAsync(
        Microsoft.EntityFrameworkCore.DbContext db, int day) =>
        await new DayTypeResolver((MPS.Infrastructure.Persistence.AppDbContext)db)
            .ResolveAsync(EmpId, D(day), CancellationToken.None);

    [Fact(DisplayName = "RESTDAY · Chưa cấu hình ⇒ mặc định T7 + CN, bất kể roster")]
    public async Task WithoutConfiguration_DefaultsToWeekend()
    {
        using var db = await SeedBaseAsync();
        // Lịch T2–T7 đầy đủ cho tuần chứa 12/07 — không được phép biến Thứ Bảy thành ngày thường.
        foreach (var day in new[] { 6, 7, 8, 9, 10, 11 })
            db.EmployeeShiftAssignments.Add(ShiftAssignment(day));
        await db.SaveChangesAsync();

        (await ResolveAsync(db, Sunday)).Should().Be(AttendanceEnums.DayType.WeeklyRestDay, "Chủ nhật");
        (await ResolveAsync(db, 11)).Should().Be(AttendanceEnums.DayType.WeeklyRestDay,
            "Thứ Bảy — mặc định T7 + CN, roster không có quyền phủ quyết");
        (await ResolveAsync(db, Wednesday)).Should().Be(AttendanceEnums.DayType.Normal, "Thứ Tư");
    }

    [Fact(DisplayName = "RESTDAY · Xưởng T2–T7 khai mask = chỉ Chủ nhật ⇒ Thứ Bảy là ngày thường (giữ ý định A2)")]
    public async Task MonToSatCompany_ConfiguresSundayOnly()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceSettings.Add(WeeklyRestSetting(DayOfWeek.Sunday));
        await db.SaveChangesAsync();

        (await ResolveAsync(db, 11)).Should().Be(AttendanceEnums.DayType.Normal,
            "công ty ấn định chỉ nghỉ Chủ nhật ⇒ Thứ Bảy là ngày làm việc bình thường, OT hôm đó 150%");
        (await ResolveAsync(db, Sunday)).Should().Be(AttendanceEnums.DayType.WeeklyRestDay);
    }

    [Fact(DisplayName = "RESTDAY · Ngày nghỉ hằng tuần có thể là thứ bất kỳ (ca luân phiên)")]
    public async Task RestDayCanBeAnyDayOfWeek()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceSettings.Add(WeeklyRestSetting(DayOfWeek.Wednesday));
        await db.SaveChangesAsync();

        (await ResolveAsync(db, Wednesday)).Should().Be(AttendanceEnums.DayType.WeeklyRestDay,
            "Đ.111 không bắt ngày nghỉ hằng tuần phải rơi vào cuối tuần");
        (await ResolveAsync(db, 11)).Should().Be(AttendanceEnums.DayType.Normal, "Thứ Bảy");
        (await ResolveAsync(db, Sunday)).Should().Be(AttendanceEnums.DayType.Normal, "Chủ nhật");
    }

    [Fact(DisplayName = "RESTDAY · ResolveRangeAsync cho cùng kết quả với ResolveAsync")]
    public async Task RangeAndSingleDay_Agree()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceSettings.Add(WeeklyRestSetting(DayOfWeek.Sunday));
        foreach (var day in new[] { 6, 7, 8, 9, 10, 11 })
            db.EmployeeShiftAssignments.Add(ShiftAssignment(day));
        await db.SaveChangesAsync();

        var range = await new DayTypeResolver(db).ResolveRangeAsync(EmpId, D(6), D(12), CancellationToken.None);

        range[D(11)].Should().Be(AttendanceEnums.DayType.Normal, "Thứ Bảy — mask chỉ khai Chủ nhật");
        range[D(12)].Should().Be(AttendanceEnums.DayType.WeeklyRestDay, "Chủ nhật");
        foreach (var day in new[] { 6, 7, 8, 9, 10, 11, 12 })
            range[D(day)].Should().Be(await ResolveAsync(db, day), $"ngày {day:D2}/07 phải khớp giữa 2 API");
    }
}
