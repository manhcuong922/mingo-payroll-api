using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Infrastructure.Services;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// A1 — Bản ghi chấm công của <b>ca OT</b> không được coi là một ngày công.
///
/// <para>Quy trình hệ thống thiết kế: ngày có tăng ca thì HR chấm công 2 lần — một bản ghi cho ca
/// chính (<c>ShiftTemplateId</c>) và một cho ca OT (<c>OvertimeRequestId</c>); entity có invariant
/// <i>exactly-one-of</i> giữa hai khoá.</para>
///
/// <para><see cref="PayrollDataCollector"/> trước đây nạp MỌI bản ghi trong kỳ nên bản ghi ca OT
/// bị tính thừa 3 lần: cộng 1 ngày công khống, giờ OT chảy vào giờ công thường, và sinh phụ trội
/// loại-ngày lần nữa. Tiền OT thì đã được trả riêng từ <c>OvertimeRequests</c> ⇒ trả hai lần.</para>
/// </summary>
public sealed class PayrollCollectorOvertimeTests
{
    [Fact(DisplayName = "A1 · Ca chính + ca OT cùng ngày ⇒ chỉ được tính 1 công")]
    public async Task OtAttendanceRecord_DoesNotAddAnExtraWorkingDay()
    {
        using var db = await SeedBaseAsync();
        // 10/07/2026 là Thứ Sáu — ngày làm việc thường.
        db.OvertimeRequests.Add(ApprovedOt(1, day: 10, new TimeOnly(18, 0), new TimeOnly(22, 0)));
        db.AttendanceRecords.AddRange(
            ShiftRecord(day: 10),                   // ca chính 8h
            OtRecord(day: 10, otRequestId: 1));     // ca OT 4h — HR chấm riêng, ĐÚNG quy trình
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongTinhCom].Should().Be(1m,
            "một ngày lịch có ca chính + ca OT vẫn chỉ là 1 ngày công; ca OT được trả riêng qua cột Tăng ca");
        inputs.Numbers[PayrollFieldKeys.CongChinhThuc].Should().Be(1m);
    }

    [Fact(DisplayName = "A1 · Giờ ca OT không được cộng vào giờ công thường (tránh trả hai lần)")]
    public async Task OtHours_DoNotLeakIntoRegularDayHours()
    {
        using var db = await SeedBaseAsync();
        db.OvertimeRequests.Add(ApprovedOt(1, day: 10, new TimeOnly(18, 0), new TimeOnly(22, 0)));
        db.AttendanceRecords.AddRange(ShiftRecord(day: 10), OtRecord(day: 10, otRequestId: 1));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.DayHours].Should().Be(8m,
            "giờ ca OT đã được trả qua cột Tăng ca; cộng tiếp vào giờ công thường là trả hai lần");
        inputs.Numbers[PayrollFieldKeys.OtTangCa].Should().BeGreaterThan(0m,
            "tiền tăng ca VẪN phải được trả — sửa A1 không được làm mất tiền OT");
    }

    [Fact(DisplayName = "A1 · OT Chủ nhật không được cộng thêm phụ trội 'giờ thường ngày nghỉ'")]
    public async Task SundayOvertime_DoesNotAlsoReceiveWeeklyRestPremium()
    {
        using var db = await SeedBaseAsync();
        // 12/07/2026 là Chủ nhật — NV KHÔNG làm ca chính, chỉ làm 4h OT theo đơn đã duyệt.
        db.OvertimeRequests.Add(ApprovedOt(1, day: 12, new TimeOnly(8, 0), new TimeOnly(12, 0)));
        db.AttendanceRecords.Add(OtRecord(day: 12, otRequestId: 1));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.WeeklyRestHours].Should().Be(0m,
            "giờ của ca OT đã hưởng hệ số 200% ngày nghỉ tuần; đếm lại thành 'giờ thường ngày nghỉ' " +
            "sẽ cộng thêm một lần phụ trội 100% nữa ⇒ trả ~300%");
        inputs.Numbers[PayrollFieldKeys.PremiumDayType].Should().Be(0m,
            "không có giờ công THƯỜNG nào trong ngày này để phát sinh phụ trội loại-ngày");
    }
}
