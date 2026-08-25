using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Infrastructure.Persistence;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// BH-14 (QĐ 595 Đ.42) — tháng NLĐ có DƯỚI 14 ngày công hưởng lương thì KHÔNG đóng
/// BHXH/BHYT/BHTN tháng đó. Engine trước đây chỉ biết chia bảo hiểm theo tỷ lệ ngày công nên người
/// nghỉ quá nửa tháng vẫn bị thu.
///
/// <para>Bộ test này ghim <b>phép đếm</b> ở collector. Cổng cắt tiền nằm ở bảng công thức
/// (<c>InsuranceMinDaysExemptionTests</c> bên MPS.Application.Tests).</para>
///
/// <para>Kỳ của fixture là 07/2026 (01/07 = Thứ Tư; T7: 04, 11, 18, 25; CN: 05, 12, 19, 26).</para>
/// </summary>
public sealed class PayrollCollectorInsuranceMinDaysTests
{
    private static decimal N(PayrollEmployeeInputs i, string key) => i.Numbers.GetValueOrDefault(key);

    /// <summary>Chấm công N ngày làm việc liên tiếp bắt đầu từ 01/07, bỏ qua T7/CN.</summary>
    private static void AddWorkdays(AppDbContext db, int count)
    {
        var added = 0;
        for (var day = 1; day <= 31 && added < count; day++)
        {
            var date = D(day);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            db.AttendanceRecords.Add(ShiftRecord(day));
            added++;
        }
    }

    // ===================== Ngưỡng 14 =====================

    [Fact(DisplayName = "BH14-01 · 13 ngày công → miễn cả khối bảo hiểm tháng đó")]
    public async Task Thirteen_Days_Is_Exempt()
    {
        using var db = await SeedBaseAsync();
        AddWorkdays(db, 13);
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.SoNgayCongHuongLuong).Should().Be(13m);
        N(i, PayrollFieldKeys.MienBhThieuNgayCong).Should().Be(1m);
    }

    [Fact(DisplayName = "BH14-02 · Đúng 14 ngày công → vẫn đóng bình thường (14 là mốc ĐỦ)")]
    public async Task Exactly_Fourteen_Days_Is_Not_Exempt()
    {
        using var db = await SeedBaseAsync();
        AddWorkdays(db, 14);
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.SoNgayCongHuongLuong).Should().Be(14m);
        N(i, PayrollFieldKeys.MienBhThieuNgayCong).Should().Be(0m,
            "luật viết 'từ 14 ngày trở lên' cho vế NGHỈ ⇒ vế LÀM đủ 14 là thuộc diện đóng");
    }

    // ===================== Đếm theo NGÀY, không theo trọng số công =====================

    [Fact(DisplayName = "BH14-03 · Nửa buổi vẫn là MỘT ngày — 13 ngày trọn + 1 nửa buổi thì không miễn")]
    public async Task HalfDay_Counts_As_A_Full_Day()
    {
        using var db = await SeedBaseAsync();
        AddWorkdays(db, 13);
        var half = ShiftRecord(day: 20, dayHours: 4m); // 20/07 là Thứ Hai
        half.Status = AttendanceStatus.HalfDay;
        db.AttendanceRecords.Add(half);
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        // Tổng công chỉ 13,5 — nếu so ngưỡng bằng tổng công thì người này bị miễn oan.
        N(i, PayrollFieldKeys.CongHuongLuong).Should().Be(13.5m);
        N(i, PayrollFieldKeys.SoNgayCongHuongLuong).Should().Be(14m);
        N(i, PayrollFieldKeys.MienBhThieuNgayCong).Should().Be(0m);
    }

    // ===================== Ngày lễ =====================

    [Fact(DisplayName = "BH14-04 · Ngày lễ nghỉ ở nhà vẫn là ngày hưởng lương (chống miễn oan tháng Tết)")]
    public async Task PublicHoliday_Counts_As_Paid_Day()
    {
        using var db = await SeedBaseAsync();
        AddWorkdays(db, 13);
        db.PublicHolidays.Add(new PublicHoliday
        {
            Name = "Lễ test",
            HolidayDate = D(20),      // Thứ Hai, không nằm trong 13 ngày đầu
            HolidayEndDate = D(20),
            IsActive = true,
            CreatedByUserId = UserId,
        });
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.SoNgayCongHuongLuong).Should().Be(14m,
            "nghỉ lễ hưởng nguyên lương (Đ.112) — bỏ lễ ra khỏi phép đếm là miễn BH oan cả công ty " +
            "vào tháng Tết, tháng đó chỉ có ~13 ngày đi làm thật");
        N(i, PayrollFieldKeys.MienBhThieuNgayCong).Should().Be(0m);
    }

    // ===================== T7/CN đi làm thật =====================

    [Fact(DisplayName = "BH14-05 · Ngày nghỉ tuần có đi làm thật vẫn được đếm")]
    public async Task Worked_Weekend_Day_Counts()
    {
        using var db = await SeedBaseAsync();
        AddWorkdays(db, 13);
        db.AttendanceRecords.Add(ShiftRecord(day: 25)); // Thứ Bảy
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.SoNgayCongHuongLuong).Should().Be(14m,
            "ngày nghỉ tuần mà vẫn đi làm thì vẫn là ngày CÓ làm việc và CÓ hưởng lương; lọc theo " +
            "thứ còn đếm sai cho ca xoay (ngày nghỉ tuần rơi vào giữa tuần)");
        N(i, PayrollFieldKeys.MienBhThieuNgayCong).Should().Be(0m);
    }

    // ===================== Vắng / nghỉ không lương =====================

    [Fact(DisplayName = "BH14-06 · Ngày vắng và nghỉ không lương không được đếm")]
    public async Task Absent_And_Unpaid_Leave_Do_Not_Count()
    {
        using var db = await SeedBaseAsync();
        AddWorkdays(db, 10);
        foreach (var day in new[] { 20, 21, 22, 23 })   // T2–T5 tuần cuối
        {
            var rec = ShiftRecord(day, dayHours: 0m);
            rec.Status = day % 2 == 0 ? AttendanceStatus.Absent : AttendanceStatus.OnLeave;
            db.AttendanceRecords.Add(rec);
        }
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.SoNgayCongHuongLuong).Should().Be(10m,
            "vắng và nghỉ KHÔNG lương đều không mang công ⇒ không phải ngày hưởng lương");
        N(i, PayrollFieldKeys.MienBhThieuNgayCong).Should().Be(1m);
    }

    // ===================== Tháng chưa kết thúc =====================

    [Fact(DisplayName = "BH14-08 · Giữa tháng (tạm tính) CHƯA được miễn — ngưỡng là phán quyết cả tháng")]
    public async Task MidMonth_Preview_Is_Not_Exempt_Yet()
    {
        using var db = await SeedBaseAsync();
        AddWorkdays(db, 5);
        await db.SaveChangesAsync();

        // Đứng ở 10/07/2026 — kỳ 07/2026 chưa xong.
        var i = await CollectAsync(db, clock: new FixedClock(new DateTime(2026, 7, 10, 3, 0, 0, DateTimeKind.Utc)));

        N(i, PayrollFieldKeys.SoNgayCongHuongLuong).Should().Be(5m);
        N(i, PayrollFieldKeys.MienBhThieuNgayCong).Should().Be(0m,
            "chưa hết tháng thì chưa kết luận được — nếu miễn luôn thì từ mùng 1 đến ~ngày 20, màn " +
            "hình tạm tính hiện bảo hiểm = 0 cho cả công ty");
    }

    [Fact(DisplayName = "BH14-07 · Nghỉ phép CÓ lương được đếm như ngày đi làm")]
    public async Task Paid_Leave_Counts()
    {
        using var db = await SeedBaseAsync();
        AddWorkdays(db, 10);
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeInformationId = EmpId,
            LeaveTypeId = 1,
            StartDate = D(20),
            EndDate = D(23),
            Status = LeaveRequestStatus.Approved,
            IsPaid = true,
            Reason = "Phép năm",
            CreatedByUserId = UserId,
        });
        foreach (var day in new[] { 20, 21, 22, 23 })
        {
            var rec = ShiftRecord(day, dayHours: 0m);
            rec.Status = AttendanceStatus.OnLeave;
            db.AttendanceRecords.Add(rec);
        }
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.SoNgayCongHuongLuong).Should().Be(14m, "phép năm hưởng nguyên lương");
        N(i, PayrollFieldKeys.MienBhThieuNgayCong).Should().Be(0m);
    }
}
