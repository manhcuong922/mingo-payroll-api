using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Infrastructure.Services;
using AttendanceEnums = MPS.Domain.Enums.Attendance;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// A2 — Ngày nghỉ tuần phải suy từ <b>bảng phân ca</b>, không hardcode Thứ Bảy + Chủ nhật.
///
/// <para>Điều 111 BLLĐ chỉ yêu cầu tối thiểu 1 ngày nghỉ/tuần và để doanh nghiệp ấn định theo lịch
/// phân ca của từng người. Công ty làm T2–T7 (rất phổ biến ở xưởng sản xuất) thì Thứ Bảy là ngày
/// làm việc bình thường; coi cứng là ngày nghỉ tuần khiến mỗi ca Thứ Bảy được trả thêm phụ trội
/// 100% và OT Thứ Bảy ăn 200% thay vì 150%.</para>
///
/// <para>Kỳ dùng chung 07/2026 — các Thứ Bảy: 04, 11, 18, 25.</para>
/// </summary>
public sealed class DayTypeResolverRestDayTests
{
    private static readonly int[] Saturdays = { 4, 11, 18, 25 };
    private const int Sunday = 12;
    private const int Wednesday = 8;

    // ===================== Tác động lên bảng lương =====================

    [Fact(DisplayName = "A2 · Thứ Bảy được xếp ca chính thức ⇒ là ngày làm việc, không phải ngày nghỉ tuần")]
    public async Task ScheduledSaturday_IsNotAWeeklyRestDay()
    {
        using var db = await SeedBaseAsync();
        // Công ty làm T2–T7: NV được PHÂN CA vào cả 4 Thứ Bảy và đi làm đủ.
        foreach (var day in Saturdays)
        {
            db.EmployeeShiftAssignments.Add(ShiftAssignment(day));
            db.AttendanceRecords.Add(ShiftRecord(day));
        }
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.WeeklyRestHours].Should().Be(0m,
            "ngày nghỉ tuần phải suy từ bảng phân ca (Đ.111 BLLĐ), không hardcode T7+CN");
        inputs.Numbers[PayrollFieldKeys.PremiumDayType].Should().Be(0m,
            "4 Thứ Bảy × 8h × 48.077đ × 100% ≈ 1,54 triệu/người/tháng trả dư nếu coi là ngày nghỉ tuần");
        inputs.Numbers[PayrollFieldKeys.CongTinhCom].Should().Be(4m, "vẫn phải được tính đủ 4 công");
    }

    [Fact(DisplayName = "A2 · OT vào Thứ Bảy đã xếp ca hưởng 150%, không phải 200%")]
    public async Task OvertimeOnScheduledSaturday_UsesNormalDayMultiplier()
    {
        using var db = await SeedBaseAsync();
        db.EmployeeShiftAssignments.Add(ShiftAssignment(11)); // 11/07/2026 — Thứ Bảy có lịch
        db.OvertimeRequests.Add(ApprovedOt(1, day: 11, new TimeOnly(18, 0), new TimeOnly(22, 0)));
        // OT chỉ ra tiền khi có bản ghi chấm công đối chứng (xem PayrollCollectorFixture.OtRecord).
        db.AttendanceRecords.Add(OtRecord(day: 11, otRequestId: 1, new TimeOnly(18, 0), new TimeOnly(22, 0)));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.OtTangCa].Should().BeApproximately(HourlyRate * 4m * 1.5m, 1m,
            "ngày Thứ Bảy có phân ca là ngày thường ⇒ OT 150%, không phải 200% của ngày nghỉ tuần");
    }

    // ===================== Hành vi của resolver =====================

    private static async Task<AttendanceEnums.DayType> ResolveAsync(
        Microsoft.EntityFrameworkCore.DbContext db, int day) =>
        await new DayTypeResolver((MPS.Infrastructure.Persistence.AppDbContext)db)
            .ResolveAsync(EmpId, D(day), CancellationToken.None);

    [Fact(DisplayName = "A2 · Chủ nhật KHÔNG có trong lịch phân ca vẫn là ngày nghỉ tuần")]
    public async Task UnscheduledSunday_RemainsWeeklyRestDay()
    {
        using var db = await SeedBaseAsync();
        // Lịch T2–T7 của tuần chứa 12/07 (T2 06 → T7 11) — Chủ nhật 12 cố ý không phân ca.
        foreach (var day in new[] { 6, 7, 8, 9, 10, 11 })
            db.EmployeeShiftAssignments.Add(ShiftAssignment(day));
        await db.SaveChangesAsync();

        (await ResolveAsync(db, Sunday)).Should().Be(AttendanceEnums.DayType.WeeklyRestDay,
            "ngày nghỉ tuần THẬT do lịch phân ca ấn định vẫn phải được nhận diện — " +
            "bản sửa A2 không được biến mọi ngày thành ngày thường");
        (await ResolveAsync(db, 11)).Should().Be(AttendanceEnums.DayType.Normal, "Thứ Bảy có lịch");
    }

    [Fact(DisplayName = "A2 · Ngày thường KHÔNG có trong lịch phân ca cũng là ngày nghỉ tuần")]
    public async Task UnscheduledWeekday_IsAlsoAWeeklyRestDay()
    {
        using var db = await SeedBaseAsync();
        // Lịch ca đêm/luân phiên: tuần 06–12/07 chỉ làm T2, T3, T5, T6 — nghỉ Thứ Tư 08.
        foreach (var day in new[] { 6, 7, 9, 10 })
            db.EmployeeShiftAssignments.Add(ShiftAssignment(day));
        await db.SaveChangesAsync();

        (await ResolveAsync(db, Wednesday)).Should().Be(AttendanceEnums.DayType.WeeklyRestDay,
            "ngày nghỉ tuần do công ty ấn định có thể rơi vào bất kỳ thứ nào (ca luân phiên), " +
            "không nhất thiết là cuối tuần");
    }

    [Fact(DisplayName = "A2 · Chưa số hoá lịch phân ca ⇒ giữ quy ước cũ T7+CN")]
    public async Task WithoutAnyRoster_FallsBackToWeekend()
    {
        using var db = await SeedBaseAsync(); // không seed EmployeeShiftAssignment nào

        (await ResolveAsync(db, Sunday)).Should().Be(AttendanceEnums.DayType.WeeklyRestDay);
        (await ResolveAsync(db, 11)).Should().Be(AttendanceEnums.DayType.WeeklyRestDay, "Thứ Bảy");
        (await ResolveAsync(db, Wednesday)).Should().Be(AttendanceEnums.DayType.Normal);
    }

    [Fact(DisplayName = "A2 · ResolveRangeAsync cho cùng kết quả với ResolveAsync")]
    public async Task RangeAndSingleDay_Agree()
    {
        using var db = await SeedBaseAsync();
        foreach (var day in new[] { 6, 7, 8, 9, 10, 11 })
            db.EmployeeShiftAssignments.Add(ShiftAssignment(day));
        await db.SaveChangesAsync();

        var range = await new DayTypeResolver(db).ResolveRangeAsync(EmpId, D(6), D(12), CancellationToken.None);

        range[D(11)].Should().Be(AttendanceEnums.DayType.Normal, "Thứ Bảy có lịch");
        range[D(12)].Should().Be(AttendanceEnums.DayType.WeeklyRestDay, "Chủ nhật không có lịch");
        foreach (var day in new[] { 6, 7, 8, 9, 10, 11, 12 })
            range[D(day)].Should().Be(await ResolveAsync(db, day), $"ngày {day:D2}/07 phải khớp giữa 2 API");
    }
}
