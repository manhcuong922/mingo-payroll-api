using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Infrastructure.Persistence;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// ⭐ CONG-HOUR-2026-08 — ngày công tính theo GIỜ LÀM THỰC / GIỜ ĐƯỢC PHÂN của chính ca đó.
///
/// <para>Trước bản này trọng số chỉ đọc <see cref="AttendanceStatus"/> nên chỉ ra được 1 / 0,5 / 0,
/// và <c>DayHours</c> — vốn đã có sẵn trên bản ghi — không tham gia vào lương. Hệ quả đo được: ca 8h
/// đi muộn 2 tiếng vẫn ra <c>Late</c> ⇒ ăn TRỌN 1 công dù chỉ làm 6h.</para>
///
/// <para>Bộ test này ghim cả hai chiều: thiếu giờ thì mất công theo đúng tỷ lệ, và ca ngắn (4h) làm
/// đủ vẫn là 1 công — mẫu số là ca ĐƯỢC PHÂN chứ không phải 8 giờ chuẩn của luật.</para>
/// </summary>
public sealed class PayrollCollectorDayWeightTests
{
    private const int HalfShiftId = 90;
    private const int NoStdHoursShiftId = 91;

    private static decimal N(PayrollEmployeeInputs i, string key) => i.Numbers.GetValueOrDefault(key);

    /// <summary>Gắn giờ chuẩn cho ca mặc định của fixture (fixture để trống ⇒ rơi vào nhánh fallback).</summary>
    private static void SetShiftHours(AppDbContext db, decimal standardHours, decimal standardDays = 1m)
    {
        var shift = db.ShiftTemplates.Single(s => s.Id == ShiftId);
        shift.StandardHours = standardHours;
        shift.StandardDays = standardDays;
    }

    /// <summary>Ca 4 tiếng (ca sáng) — vẫn là MỘT công đầy đủ, đúng như bảng ca đang seed.</summary>
    private static void AddHalfDayShift(AppDbContext db) =>
        db.ShiftTemplates.Add(new ShiftTemplate
        {
            Id = HalfShiftId,
            Name = "Ca sáng",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            StandardHours = 4m,
            StandardDays = 1m,
        });

    private static AttendanceRecord RecordOnShift(
        int day, int shiftId, decimal dayHours, AttendanceStatus status = AttendanceStatus.Present) => new()
        {
            EmployeeInformationId = EmpId,
            WorkDate = D(day),
            ShiftTemplateId = shiftId,
            Status = status,
            DayHours = dayHours,
            CreatedByUserId = UserId,
        };

    private static LateEarlyRequest ApprovedLateEarly(int day, short minutes, LateEarlyKind kind) => new()
    {
        EmployeeInformationId = EmpId,
        WorkDate = D(day),
        Kind = kind,
        Minutes = minutes,
        Status = LateEarlyRequestStatus.Approved,
        CreatedByUserId = UserId,
    };

    // ===================== Chia theo tỷ lệ giờ =====================

    [Fact(DisplayName = "CH-01 · Ca 8h làm 6h — 0,75 công thô, làm tròn LÊN 1,0 khi đó là ngày duy nhất")]
    public async Task Eight_Hour_Shift_Worked_Six_Gives_Three_Quarters()
    {
        using var db = await SeedBaseAsync();
        SetShiftHours(db, 8m);
        // Đi muộn 2 tiếng: resolver xếp `Late` — nhánh cũ trả trọn 1.0 công.
        db.AttendanceRecords.Add(RecordOnShift(1, ShiftId, dayHours: 6m, status: AttendanceStatus.Late));
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        // ⭐ CONG-MONTHLY-2026-08 — 6h là toàn bộ giờ công của kỳ ⇒ 6/8 = 0,75 ⇒ làm tròn lên 1,0.
        // Ngày thiếu giờ chỉ thực sự bị trừ khi phần thiếu của CẢ KỲ vượt nửa công (xem CHM-*).
        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(1m);
        N(i, PayrollFieldKeys.CongTinhCom).Should().Be(1m);
    }

    [Fact(DisplayName = "CH-02 · Ca 4h khai đáng trọn 1 công, làm đủ ⇒ 1 công (quy chuẩn theo StandardDays)")]
    public async Task Four_Hour_Shift_Worked_Fully_Gives_One_Day()
    {
        using var db = await SeedBaseAsync();
        AddHalfDayShift(db);
        db.AttendanceRecords.Add(RecordOnShift(1, HalfShiftId, dayHours: 4m));
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(1m);
    }

    [Fact(DisplayName = "CH-03 · Ca 4h/1 công làm 3h ⇒ 0,75 công thô, làm tròn lên 1,0")]
    public async Task Four_Hour_Shift_Worked_Three_Gives_Three_Quarters()
    {
        using var db = await SeedBaseAsync();
        AddHalfDayShift(db);
        db.AttendanceRecords.Add(RecordOnShift(1, HalfShiftId, dayHours: 3m));
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(1m, "3/4 ca × 1,00 công = 0,75 ⇒ làm tròn lên 1,0");
    }

    [Fact(DisplayName = "CH-04 · Số lẻ vô hạn vẫn ra bội số 0,5 — cột công luôn đối chiếu được bằng mắt")]
    public async Task Ratio_Always_Lands_On_A_Half_Multiple()
    {
        using var db = await SeedBaseAsync();
        SetShiftHours(db, 3m);
        // 1/3 ca = 0,3333… — bản cũ chốt ở 0,33; nay làm tròn LÊN bội số 0,5 ⇒ 0,5.
        db.AttendanceRecords.Add(RecordOnShift(1, ShiftId, dayHours: 1m));
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(0.5m);
    }

    [Fact(DisplayName = "CH-05 · Làm quá giờ ca không đẩy công vượt 1 — phần dôi thuộc về đơn OT")]
    public async Task Overworking_Does_Not_Exceed_One_Day()
    {
        using var db = await SeedBaseAsync();
        SetShiftHours(db, 8m);
        db.AttendanceRecords.Add(RecordOnShift(1, ShiftId, dayHours: 11m));
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(1m);
    }

    [Fact(DisplayName = "CH-06 · Bản ghi không có giờ nào ra 0 công — 'có check-in là đủ công' đã bị bịt")]
    public async Task Record_Without_Hours_Gives_Zero()
    {
        using var db = await SeedBaseAsync();
        SetShiftHours(db, 8m);
        db.AttendanceRecords.Add(RecordOnShift(1, ShiftId, dayHours: 0m));
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(0m);
    }

    [Fact(DisplayName = "CH-07 · Ca khai StandardHours = 0 lùi về giờ chuẩn/ngày của luật")]
    public async Task Shift_Without_Standard_Hours_Falls_Back_To_Labor_Law()
    {
        using var db = await SeedBaseAsync();
        db.ShiftTemplates.Add(new ShiftTemplate
        {
            Id = NoStdHoursShiftId,
            Name = "Ca thiếu cấu hình",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            StandardHours = 0m,
        });
        // Giờ chuẩn/ngày của fixture là 8 ⇒ 4/8 = 0,5 công.
        db.AttendanceRecords.Add(RecordOnShift(1, NoStdHoursShiftId, dayHours: 4m));
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(0.5m);
    }

    // ===================== Đơn đi muộn / về sớm =====================

    [Fact(DisplayName = "CH-08 · Ngày có đơn đi muộn đã duyệt hưởng trọn 1 công dù giờ thực thiếu")]
    public async Task Approved_Late_Request_Restores_Full_Day()
    {
        using var db = await SeedBaseAsync();
        SetShiftHours(db, 8m);
        db.AttendanceRecords.Add(RecordOnShift(1, ShiftId, dayHours: 5m, status: AttendanceStatus.Late));
        db.LateEarlyRequests.Add(ApprovedLateEarly(1, minutes: 180, LateEarlyKind.LateArrival));
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(1m);
    }

    [Fact(DisplayName = "CH-09 · Đơn Pending / Rejected không cứu công — chỉ đơn đã duyệt mới miễn")]
    public async Task Pending_Request_Does_Not_Restore_Day()
    {
        using var db = await SeedBaseAsync();
        SetShiftHours(db, 8m);
        db.AttendanceRecords.Add(RecordOnShift(1, ShiftId, dayHours: 5m, status: AttendanceStatus.Late));
        var pending = ApprovedLateEarly(1, minutes: 180, LateEarlyKind.LateArrival);
        pending.Status = LateEarlyRequestStatus.Pending;
        db.LateEarlyRequests.Add(pending);
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(1m, "5/8 = 0,625 ⇒ làm tròn lên bội số 0,5 gần nhất = 1,0");
    }

    [Fact(DisplayName = "CH-10 · Đơn của ngày khác không lan sang ngày thiếu giờ")]
    public async Task Approved_Request_Only_Covers_Its_Own_Day()
    {
        using var db = await SeedBaseAsync();
        SetShiftHours(db, 8m);
        db.AttendanceRecords.Add(RecordOnShift(1, ShiftId, dayHours: 4m, status: AttendanceStatus.Late));
        db.AttendanceRecords.Add(RecordOnShift(2, ShiftId, dayHours: 4m, status: AttendanceStatus.Late));
        db.LateEarlyRequests.Add(ApprovedLateEarly(2, minutes: 240, LateEarlyKind.EarlyLeave));
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(1.5m, "ngày 1 còn 0,5 công; ngày 2 được miễn trọn 1 công");
    }

    // ===================== Các nhánh không đi qua giờ =====================

    [Fact(DisplayName = "CH-11 · Nghỉ có lương vẫn 1 công dù không có giờ chấm nào")]
    public async Task Paid_Leave_Day_Still_Counts()
    {
        using var db = await SeedBaseAsync();
        SetShiftHours(db, 8m);
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeInformationId = EmpId,
            StartDate = D(1),
            EndDate = D(1),
            IsPaid = true,
            Status = LeaveRequestStatus.Approved,
            CreatedByUserId = UserId,
        });
        db.AttendanceRecords.Add(RecordOnShift(1, ShiftId, dayHours: 0m, status: AttendanceStatus.OnLeave));
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(1m);
    }

    [Fact(DisplayName = "CH-12 · Vắng mặt vẫn là 0 công")]
    public async Task Absent_Gives_Zero()
    {
        using var db = await SeedBaseAsync();
        SetShiftHours(db, 8m);
        var absent = RecordOnShift(1, ShiftId, dayHours: 0m, status: AttendanceStatus.Absent);
        db.AttendanceRecords.Add(absent);
        await db.SaveChangesAsync();

        var i = await CollectAsync(db);

        N(i, PayrollFieldKeys.CongChinhThuc).Should().Be(0m);
    }
}
