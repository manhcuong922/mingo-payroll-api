using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Domain.Entities.Attendance;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// CONG-MONTHLY-2026-08 — cộng GIỜ công của CẢ KỲ rồi mới chia giờ chuẩn/ngày và làm tròn LÊN bội
/// số 0,5, thay vì làm tròn ở từng ngày.
///
/// <para>Đây là test ĐẦU–CUỐI qua collector: nó chốt rằng engine nạp đủ hai thứ mà
/// <see cref="MPS.Application.Common.Services.WorkDayCongCalculator"/> cần —
/// <c>AttendanceSetting</c> (grace) và <c>ShiftTemplate.StartTime/EndTime</c> (để đo lệch) — và
/// rằng phần thiếu vụn của từng ngày được gộp lại trước khi làm tròn.</para>
///
/// <para>Kỳ 07/2026 có <b>23 ngày T2–T6</b>; ca hành chính 08:00–17:00 nghỉ trưa 12:00–13:00
/// (<c>StandardHours = 8</c>).</para>
/// </summary>
public sealed class PayrollCollectorGraceTests
{
    /// <summary>Toàn bộ ngày T2–T6 của tháng 07/2026.</summary>
    private static readonly int[] Weekdays =
    {
        1, 2, 3, 6, 7, 8, 9, 10, 13, 14, 15, 16, 17, 20, 21, 22, 23, 24, 27, 28, 29, 30, 31,
    };

    private static AttendanceSetting GraceSetting(int lateGrace, int earlyGrace) => new()
    {
        DailyEditCutoffTime = new TimeOnly(18, 0),
        LateGraceMinutes = lateGrace,
        EarlyLeaveGraceMinutes = earlyGrace,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    /// <summary>Chấm công MỌI ngày T2–T6 của kỳ với cùng một khung giờ, trừ <paramref name="skipDay"/>.</summary>
    private static void FillMonth(
        MPS.Infrastructure.Persistence.AppDbContext db, TimeOnly checkIn, TimeOnly checkOut, int? skipDay = null)
    {
        foreach (var day in Weekdays)
        {
            if (day == skipDay) continue;
            db.AttendanceRecords.Add(ShiftRecordAt(day, checkIn, checkOut));
        }
    }

    [Fact(DisplayName = "CHỐT · Đi làm đủ 23 ngày T2–T6 ⇒ ra ĐÚNG 23 công, không hụt ngày nào")]
    public async Task FullAttendance_MatchesStandardWorkingDays()
    {
        using var db = await SeedBaseAsync();
        FillMonth(db, new TimeOnly(8, 0), new TimeOnly(17, 0));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongChinhThuc].Should().Be(23m,
            "23 ngày × 8h = 184h ÷ 8 = 23,00 — đây là tiêu chí nghiệm thu của cả cách tính công");
    }

    [Fact(DisplayName = "GRACE · Muộn 5' mỗi ngày (trong grace mặc định) ⇒ vẫn đủ 23 công")]
    public async Task LateWithinDefaultGraceEveryDay_StillFullMonth()
    {
        using var db = await SeedBaseAsync(); // không seed AttendanceSetting ⇒ grace mặc định 5'
        FillMonth(db, new TimeOnly(8, 5), new TimeOnly(17, 0));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongChinhThuc].Should().Be(23m,
            "hệ thống ghi nhận Present (trong grace) thì bảng lương không được trừ công");
    }

    [Fact(DisplayName = "GỘP · Muộn 1h mỗi ngày cả tháng ⇒ 20,5 công (dồn phần thiếu rồi mới làm tròn)")]
    public async Task DailyShortfalls_AccumulateBeforeRounding()
    {
        using var db = await SeedBaseAsync();
        FillMonth(db, new TimeOnly(9, 0), new TimeOnly(17, 0)); // 7h/ngày
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongChinhThuc].Should().Be(20.5m,
            "23 × 7h = 161h ÷ 8 = 20,125 ⇒ làm tròn lên 20,5");
    }

    [Fact(DisplayName = "GỘP · Muộn 6' mỗi ngày (vượt grace) vẫn đủ 23 công — phần thiếu chưa tới nửa công")]
    public async Task TinyDailyShortfalls_AreAbsorbedByRoundingUp()
    {
        using var db = await SeedBaseAsync();
        FillMonth(db, new TimeOnly(8, 6), new TimeOnly(17, 0)); // 7,9h/ngày
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongChinhThuc].Should().Be(23m,
            "23 × 7,9 = 181,7h ÷ 8 = 22,71 ⇒ làm tròn lên 23. Làm tròn LÊN luôn nghiêng về NLĐ; " +
            "muốn phạt đi muộn thì phải dùng cơ chế khác, không phải cơ chế công");
    }

    [Fact(DisplayName = "GRACE · Cấu hình grace 15' ⇒ collector đọc đúng cấu hình, không dùng hằng số")]
    public async Task ConfiguredGrace_IsHonoured()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceSettings.Add(GraceSetting(lateGrace: 15, earlyGrace: 15));
        FillMonth(db, new TimeOnly(8, 12), new TimeOnly(17, 0));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongChinhThuc].Should().Be(23m,
            "muộn 12' nằm trong grace 15' đã cấu hình ⇒ được công nhận trọn ca mỗi ngày");
    }

    [Fact(DisplayName = "GRACE · Grace tắt (0') ⇒ muộn 30' mỗi ngày bị trừ đúng theo giờ thực")]
    public async Task GraceDisabled_DeductsByActualHours()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceSettings.Add(GraceSetting(lateGrace: 0, earlyGrace: 0));
        FillMonth(db, new TimeOnly(8, 30), new TimeOnly(17, 0)); // 7,5h/ngày
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongChinhThuc].Should().Be(22m,
            "23 × 7,5 = 172,5h ÷ 8 = 21,5625 ⇒ làm tròn lên 22");
    }

    [Fact(DisplayName = "TRẦN NGÀY · Ở lại 12h một hôm KHÔNG đẩy công vượt — phần dôi thuộc đơn OT")]
    public async Task OverstayingOneDay_DoesNotInflateMonthlyCong()
    {
        using var db = await SeedBaseAsync();
        FillMonth(db, new TimeOnly(8, 0), new TimeOnly(17, 0), skipDay: 21);
        // Ngày 21 kéo tới 21:00 (12h thực) — không có đơn OT nào đối chứng.
        db.AttendanceRecords.Add(ShiftRecordAt(21, new TimeOnly(8, 0), new TimeOnly(21, 0)));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.CongChinhThuc].Should().Be(23m,
            "không kẹp trần theo ngày thì 4h dôi sẽ thành 0,5 công khống mà không cần đơn OT nào — " +
            "vừa trả sai vừa lách được OvertimeCapChecker");
    }
}
