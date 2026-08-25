using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Infrastructure.Persistence;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// ⭐ CC-2026-08 — công chuẩn KHÔNG còn trừ ngày lễ khỏi mẫu số; bù lại ngày lễ được cộng vào TỬ SỐ
/// như một ngày công hưởng lương (Đ.112 BLLĐ).
///
/// <para>Hai vế phải đi cùng nhau: chỉ đổi mẫu số mà quên tử số thì NLĐ mất trắng lương ngày lễ
/// (tháng Tết ~23% lương tháng). Bộ test này ghim vế TỬ SỐ.</para>
///
/// <para>Trọng tâm là <b>chống độn công</b>: bù theo phần CÒN THIẾU của ngày
/// (<c>1 − trọng số đã ghi nhận</c>) chứ không phải "chưa có bản ghi thì cộng 1" — một ngày có thể
/// có nhiều bản ghi (ca xoay) và <c>HalfDay</c> chỉ mang 0.5 công.</para>
///
/// <para>Kỳ của fixture là 07/2026 (01/07 = Thứ Tư). Tháng 7 không có mốc lễ dương lịch cố định nào
/// nên mọi ngày lễ trong test đều do test tự nhập vào bảng <c>PublicHolidays</c> — đúng đường đi
/// thật của Tết Âm lịch / Giỗ Tổ mà HR phải nhập tay.</para>
/// </summary>
public sealed class PayrollCollectorHolidayCreditTests
{
    /// <summary>Nhập 1 ngày lễ (đúng 1 ngày) vào bảng PublicHolidays của kỳ 07/2026.</summary>
    private static void AddHoliday(AppDbContext db, int day, string name = "Lễ test")
        => db.PublicHolidays.Add(new PublicHoliday
        {
            Name = name,
            HolidayDate = D(day),
            HolidayEndDate = D(day),
            IsActive = true,
            CreatedByUserId = UserId,
        });

    private static decimal N(PayrollEmployeeInputs i, string key) => i.Numbers.GetValueOrDefault(key);

    // ===================== Bù công cho ngày lễ KHÔNG đi làm =====================

    [Fact(DisplayName = "CC-01 · Nghỉ lễ ở nhà vẫn được cộng 1 công hưởng lương")]
    public async Task Holiday_NotWorked_Credits_One_Day()
    {
        using var db = await SeedBaseAsync();
        AddHoliday(db, 15); // Thứ Tư
        db.AttendanceRecords.Add(ShiftRecord(day: 13));
        db.AttendanceRecords.Add(ShiftRecord(day: 14));
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongLe).Should().Be(1m, "ngày lễ là ngày hưởng nguyên lương — Đ.112 BLLĐ");
        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(3m, "2 ngày đi làm + 1 ngày lễ");
    }

    [Fact(DisplayName = "CC-02 · Không chấm công ngày nào trong kỳ vẫn được hưởng công ngày lễ")]
    public async Task Holiday_Credited_Even_With_Zero_Attendance()
    {
        using var db = await SeedBaseAsync();
        AddHoliday(db, 15);
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        // Quyết định PO 19/08/2026: không đòi NLĐ phải có ngày công nào, chỉ cần HĐ còn hiệu lực.
        N(i, PayrollFieldKeys.CongLe).Should().Be(1m);
        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(1m);
    }

    // ===================== Chống độn công khi ĐI LÀM ngày lễ =====================

    [Fact(DisplayName = "CC-03 · Đi làm trọn ca ngày lễ KHÔNG bị độn thành 2 công")]
    public async Task Holiday_FullyWorked_Is_Not_Double_Counted()
    {
        using var db = await SeedBaseAsync();
        AddHoliday(db, 15);
        db.AttendanceRecords.Add(ShiftRecord(day: 15)); // đi làm đúng ngày lễ
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongLe).Should().Be(0m,
            "ngày đó đã có 1.0 công từ bản ghi chấm công — cộng thêm là trả 2 lần");
        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(1m,
            "quyết định PO: giữ 300% (1 công + phụ trội 200%), không nâng lên 400%");
    }

    [Fact(DisplayName = "CC-04 · Làm nửa ca ngày lễ chỉ được bù nửa công còn thiếu")]
    public async Task Holiday_HalfDay_Credits_Only_The_Missing_Half()
    {
        using var db = await SeedBaseAsync();
        AddHoliday(db, 15);
        var half = ShiftRecord(day: 15, dayHours: 4m);
        half.Status = AttendanceStatus.HalfDay;
        db.AttendanceRecords.Add(half);
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongLe).Should().Be(0.5m, "HalfDay chỉ mang 0.5 công ⇒ còn thiếu 0.5");
        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(1m);
    }

    [Fact(DisplayName = "CC-05 · Hai ca trong cùng một ngày lễ không bị bù thêm công")]
    public async Task Holiday_With_Two_Shifts_Gets_No_Credit()
    {
        using var db = await SeedBaseAsync();
        AddHoliday(db, 15);
        db.ShiftTemplates.Add(new ShiftTemplate
        {
            Id = 2,
            Name = "Ca đêm",
            StartTime = new TimeOnly(22, 0),
            EndTime = new TimeOnly(6, 0),
        });
        var ca1 = ShiftRecord(day: 15);
        var ca2 = ShiftRecord(day: 15);
        ca2.ShiftTemplateId = 2;
        db.AttendanceRecords.AddRange(ca1, ca2);
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        // Unique index là bộ ba (Employee, WorkDate, ShiftTemplate) nên 1 ngày có thể có nhiều bản
        // ghi ⇒ guard phải cộng dồn TRỌNG SỐ theo ngày, không kiểm tra "đã có bản ghi chưa".
        N(i, PayrollFieldKeys.CongLe).Should().Be(0m);
        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(2m, "giữ nguyên hành vi cũ với ca xoay");
    }

    // ===================== Ranh giới: cuối tuần / ngoài hợp đồng =====================

    [Fact(DisplayName = "CC-06 · Ngày lễ rơi vào Thứ Bảy/Chủ nhật KHÔNG được cộng công")]
    public async Task Holiday_On_Weekend_Is_Not_Credited()
    {
        using var db = await SeedBaseAsync();
        AddHoliday(db, 18); // Thứ Bảy
        AddHoliday(db, 26, "Lễ CN"); // Chủ nhật
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        // Công chuẩn chỉ đếm T2–T6; cộng ngày lễ cuối tuần vào tử số sẽ vượt mẫu số ⇒ trả thừa.
        N(i, PayrollFieldKeys.CongLe).Should().Be(0m);
    }

    [Fact(DisplayName = "CC-07 · Ngày lễ nằm ngoài kỳ hiệu lực hợp đồng KHÔNG được cộng công")]
    public async Task Holiday_Outside_Contract_Coverage_Is_Not_Credited()
    {
        using var db = await SeedBaseAsync();
        // NV vào làm từ 20/07 ⇒ ngày lễ 15/07 xảy ra trước khi có hợp đồng.
        db.Contracts.Single().EffectiveFrom = new DateTime(2026, 7, 20);
        AddHoliday(db, 15);
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongLe).Should().Be(0m, "#25 — NV vào giữa tháng không hưởng lễ trước đó");
    }

    // ===================== Tách CONGTINHCOM (cơm) khỏi CONGHUONGLUONG (nền BH) =====================

    [Fact(DisplayName = "CC-08 · Công tính cơm KHÔNG gồm công ngày lễ, công hưởng lương thì CÓ")]
    public async Task Meal_Days_Exclude_Holiday_But_Paid_Days_Include_It()
    {
        using var db = await SeedBaseAsync();
        AddHoliday(db, 15);
        db.AttendanceRecords.Add(ShiftRecord(day: 13));
        db.AttendanceRecords.Add(ShiftRecord(day: 14));
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongTinhCom).Should().Be(2m,
            "nghỉ lễ ở nhà thì không ăn cơm ca — tiền cơm chia theo ngày ĐI LÀM THẬT");
        N(i, PayrollFieldKeys.CongHuongLuong).Should().Be(3m,
            "nền đóng BH phải gồm công lễ, nếu không sẽ đóng thiếu đúng bằng tỉ lệ ngày lễ");
    }

    // ===================== Bản vá kèm: phép có lương trùng ngày lễ =====================

    [Fact(DisplayName = "CC-09 · Đơn phép có lương trùng ngày lễ không còn mất công")]
    public async Task Paid_Leave_Falling_On_Holiday_Still_Gets_Credit()
    {
        using var db = await SeedBaseAsync();
        AddHoliday(db, 15);
        db.LeaveTypes.Add(new LeaveType { Id = 1, Code = "ANNUAL", Name = "Phép năm", IsPaid = true });
        db.LeaveRequests.Add(new LeaveRequest
        {
            Id = 1,
            EmployeeInformationId = EmpId,
            LeaveTypeId = 1,
            StartDate = D(15),
            EndDate = D(15),
            IsPaid = true,
            Status = LeaveRequestStatus.Approved,
            CreatedByUserId = UserId,
        });
        // Duyệt phép sinh bản ghi OnLeave 0h cho ngày đó.
        var onLeave = ShiftRecord(day: 15, dayHours: 0m);
        onLeave.Status = AttendanceStatus.OnLeave;
        db.AttendanceRecords.Add(onLeave);
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        // Resolver ưu tiên PublicHoliday > PaidLeaveDay nên `DayWeight(OnLeave, PublicHoliday)` = 0
        // ⇒ trước CC-2026-08 ngày này mất trắng công.
        N(i, PayrollFieldKeys.CongLe).Should().Be(1m);
        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(1m);
    }

    // ===================== Không được đụng vào giờ công / phụ trội =====================

    [Fact(DisplayName = "CC-10 · Nghỉ lễ ở nhà KHÔNG sinh giờ công và KHÔNG được phụ trội ngày lễ")]
    public async Task Holiday_NotWorked_Adds_No_Hours_And_No_Premium()
    {
        using var db = await SeedBaseAsync();
        AddHoliday(db, 15);
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.TotalWorkHours).Should().Be(0m,
            "cộng giờ ảo sẽ sai trần OT và sai bậc hỗ trợ tiến độ của HĐ khoán");
        N(i, PayrollFieldKeys.PremiumDayType).Should().Be(0m,
            "phụ trội 200% chỉ dành cho người THỰC SỰ đi làm ngày lễ");
    }

    // ===================== HĐ thử việc / khoán =====================

    [Fact(DisplayName = "CC-11 · Ngày lễ trong đoạn thử việc được cộng vào công thử việc")]
    public async Task Holiday_During_Probation_Credits_Probation_Days()
    {
        using var db = await SeedBaseAsync();
        db.Contracts.Single().Kind = ContractEnums.ContractKind.Probation;
        AddHoliday(db, 15);
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongThuViec).Should().Be(1m);
        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(0m);
    }

    [Fact(DisplayName = "CC-12 · HĐ khoán việc cũng được cộng công ngày lễ (quyết định PO 19/08)")]
    public async Task Holiday_Credited_For_PieceWork_Contract()
    {
        using var db = await SeedBaseAsync();
        db.Contracts.Single().Kind = ContractEnums.ContractKind.PieceWork;
        AddHoliday(db, 15);
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongLe).Should().Be(1m, "PO chốt: áp dụng cho MỌI loại hợp đồng");
    }
}
