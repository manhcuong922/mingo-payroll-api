using FluentAssertions;
using MPS.Application.Common.Services;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// CONG-MONTHLY-2026-08 — <see cref="WorkDayCongCalculator.RecognizedHours"/>: GIỜ CÔNG ĐƯỢC CÔNG
/// NHẬN của MỘT ngày. Đây là bước 1; việc cộng cả kỳ rồi mới làm tròn nằm ở
/// <see cref="MonthlyCongRoundingTests"/>.
///
/// <para>Cố ý kiểm bằng GIỜ chứ không quy ra công: quy ra công ở mức từng ngày là đúng thứ mô hình
/// mới bỏ đi — 6h lẻ một ngày mà làm tròn ngay sẽ thành 1 công, trong khi cộng vào cả tháng thì nó
/// chỉ là 6 giờ.</para>
///
/// <para>Ca dùng chung: hành chính 08:00–17:00, nghỉ trưa 12:00–13:00 ⇒ <c>StandardHours = 8</c>.
/// Grace mặc định 5 phút cả hai chiều.</para>
/// </summary>
public sealed class WorkDayCongCalculatorTests
{
    private static readonly DateOnly WorkDate = new(2026, 7, 21); // Thứ Ba

    private static ShiftTemplate OfficeShift(decimal standardHours = 8m) => new()
    {
        Id = 1,
        Code = "HC",
        Name = "Ca hành chính",
        StartTime = new TimeOnly(8, 0),
        EndTime = new TimeOnly(17, 0),
        BreakStartTime = new TimeOnly(12, 0),
        BreakEndTime = new TimeOnly(13, 0),
        StandardHours = standardHours,
        StandardDays = 1m,
        AppliedAttendanceRuleId = 1,
        IsActive = true,
    };

    private static decimal Hours(in WorkDayInput input) => WorkDayCongCalculator.RecognizedHours(input);

    /// <summary>
    /// Dựng input từ giờ check-in/out THẬT: giờ công = khoảng chấm trừ 1h nghỉ trưa, đúng như
    /// <c>WorkHourSplitter</c> tính rồi lưu vào bản ghi.
    /// </summary>
    private static WorkDayInput From(
        TimeOnly checkIn,
        TimeOnly checkOut,
        AttendanceStatus status = AttendanceStatus.Present,
        DayType dayType = DayType.Normal,
        ShiftTemplate? shift = null,
        bool hasApprovedLateEarly = false,
        int lateGrace = 5,
        int earlyGrace = 5)
    {
        var worked = (decimal)(checkOut - checkIn).TotalHours - 1m; // trừ nghỉ trưa
        return new WorkDayInput(
            Status: status,
            DayType: dayType,
            WorkDate: WorkDate,
            CheckInAt: WorkDate.ToDateTime(checkIn),
            CheckOutAt: WorkDate.ToDateTime(checkOut),
            DayHours: Math.Round(worked, 2),
            NightHours: 0m,
            HasApprovedLateEarly: hasApprovedLateEarly,
            Shift: shift ?? OfficeShift(),
            StandardHoursPerDay: 8m,
            LateGraceMinutes: lateGrace,
            EarlyLeaveGraceMinutes: earlyGrace);
    }

    // ===================== Grace phải có hiệu lực với TIỀN =====================

    [Fact(DisplayName = "GRACE · Đi muộn 5' (trong grace) ⇒ được công nhận TRỌN 8h")]
    public void LateWithinGrace_CreditsFullShift()
    {
        Hours(From(new TimeOnly(8, 5), new TimeOnly(17, 0))).Should().Be(8m,
            "AttendanceStatusResolver đã kết luận Present; nếu công vẫn tính theo 7,92h thì hệ thống " +
            "nói 'đúng giờ' mà bảng lương vẫn trừ tiền, không có dòng nào giải thích");
    }

    [Fact(DisplayName = "GRACE · Về sớm 5' (trong grace) ⇒ được công nhận TRỌN 8h")]
    public void EarlyLeaveWithinGrace_CreditsFullShift()
    {
        Hours(From(new TimeOnly(8, 0), new TimeOnly(16, 55))).Should().Be(8m);
    }

    [Fact(DisplayName = "GRACE · Muộn 5' + về sớm 5' đều trong grace ⇒ vẫn TRỌN 8h")]
    public void LateAndEarlyBothWithinGrace_CreditsFullShift()
    {
        Hours(From(new TimeOnly(8, 5), new TimeOnly(16, 55))).Should().Be(8m);
    }

    [Fact(DisplayName = "GRACE · Muộn 6' (vượt grace) ⇒ KHÔNG tha phần nào, giữ giờ thực")]
    public void LateBeyondGrace_ForgivesNothing()
    {
        // 08:06 → 17:00 = 8h54 − 1h nghỉ trưa = 7,9h.
        Hours(From(new TimeOnly(8, 6), new TimeOnly(17, 0))).Should().Be(7.9m,
            "grace là ngưỡng THA trọn gói, không phải khoản trừ — cùng ngữ nghĩa với " +
            "AttendanceStatusResolver (muộn > grace ⇒ Late)");
    }

    [Fact(DisplayName = "GRACE · Grace tắt (0 phút) ⇒ muộn 30' giữ đúng 7,5h")]
    public void GraceDisabled_NoForgiveness()
    {
        Hours(From(new TimeOnly(8, 30), new TimeOnly(17, 0), lateGrace: 0, earlyGrace: 0))
            .Should().Be(7.5m);
    }

    // ===================== Trần theo NGÀY =====================

    [Fact(DisplayName = "TRẦN · Ở lại quá giờ ca ⇒ kẹp ở 8h, phần dôi phải đi đường đơn OT")]
    public void OverstayingShift_IsCappedAtShiftHours()
    {
        Hours(From(new TimeOnly(8, 0), new TimeOnly(19, 0))).Should().Be(8m,
            "không kẹp thì ở lại 12h một hôm sẽ đẩy công vượt công chuẩn mà không cần đơn OT nào — " +
            "vừa trả sai vừa lách được OvertimeCapChecker");
    }

    [Fact(DisplayName = "CÔNG · Về sớm 2h ⇒ chỉ được công nhận 6h")]
    public void LeavingEarly_CreditsActualHours()
    {
        Hours(From(new TimeOnly(8, 0), new TimeOnly(15, 0))).Should().Be(6m);
    }

    [Fact(DisplayName = "QUY CHUẨN · Ca 4h khai ĐÁNG TRỌN 1 công ⇒ quy về 8h của ngày công chuẩn")]
    public void ShortShiftWorthAFullCong_NormalisesToStandardDay()
    {
        var shift = OfficeShift(standardHours: 4m); // StandardDays = 1 ⇒ ca này đáng trọn 1 công
        Hours(From(new TimeOnly(8, 0), new TimeOnly(13, 0), shift: shift)).Should().Be(8m,
            "cộng dồn cả kỳ thì các ca dài ngắn phải quy về cùng đơn vị; giữ nguyên 4h sẽ làm ca " +
            "này tụt còn nửa công, trái với cấu hình StandardDays = 1,00");
    }

    [Fact(DisplayName = "QUY CHUẨN · Ca 4h khai ĐÁNG NỬA công ⇒ quy về 4h")]
    public void ShortShiftWorthHalfACong_NormalisesToHalfStandardDay()
    {
        var shift = OfficeShift(standardHours: 4m);
        shift.StandardDays = 0.5m;

        Hours(From(new TimeOnly(8, 0), new TimeOnly(13, 0), shift: shift)).Should().Be(4m);
    }

    [Fact(DisplayName = "QUY CHUẨN · Ca 4h/1 công làm nửa ca (2h) ⇒ quy về 4h")]
    public void ShortShiftPartlyWorked_NormalisesProportionally()
    {
        var shift = OfficeShift(standardHours: 4m);

        // 08:00 → 11:00 = 3h − 1h nghỉ trưa (rơi ngoài khoảng nên không trừ) ⇒ helper trừ 1h ⇒ 2h.
        Hours(From(new TimeOnly(8, 0), new TimeOnly(11, 0), shift: shift)).Should().Be(4m,
            "2/4 ca × 1,00 công × 8h = 4h ⇒ nửa công");
    }

    // ===================== Ba nhánh không đi qua giờ chấm =====================

    [Fact(DisplayName = "MIỄN · Ngày có đơn đi muộn/về sớm ĐÃ DUYỆT ⇒ trọn ca dù thiếu giờ")]
    public void ApprovedLateEarlyRequest_CreditsFullShift()
    {
        Hours(From(new TimeOnly(11, 0), new TimeOnly(17, 0), hasApprovedLateEarly: true))
            .Should().Be(8m, "quyết định PO 24/08/2026 — approver chịu trách nhiệm về phần giờ được miễn");
    }

    [Fact(DisplayName = "NGHỈ · OnLeave trên ngày nghỉ CÓ LƯƠNG ⇒ trọn ca")]
    public void OnLeave_OnPaidLeaveDay_CreditsFullShift()
    {
        var input = From(new TimeOnly(8, 0), new TimeOnly(17, 0),
            status: AttendanceStatus.OnLeave, dayType: DayType.PaidLeaveDay);

        Hours(input).Should().Be(8m,
            "phép năm đã trừ quỹ mà vẫn 0 công thì phép 'có lương' không được trả đồng nào");
    }

    [Fact(DisplayName = "NGHỈ · OnLeave trên ngày nghỉ KHÔNG lương ⇒ 0h")]
    public void OnLeave_UnpaidDay_CreditsNothing()
    {
        var input = From(new TimeOnly(8, 0), new TimeOnly(17, 0),
            status: AttendanceStatus.OnLeave, dayType: DayType.Normal);

        Hours(input).Should().Be(0m);
    }

    [Fact(DisplayName = "VẮNG · Absent ⇒ 0h dù bản ghi có giờ")]
    public void Absent_CreditsNothing()
    {
        Hours(From(new TimeOnly(8, 0), new TimeOnly(17, 0), status: AttendanceStatus.Absent))
            .Should().Be(0m);
    }

    // ===================== Dữ liệu hỏng =====================

    [Fact(DisplayName = "HỎNG · Quên chấm giờ ra (0 giờ công) ⇒ 0h, KHÔNG tự cho trọn ca")]
    public void MissingCheckOut_CreditsNothing()
    {
        var input = From(new TimeOnly(8, 0), new TimeOnly(17, 0)) with
        {
            CheckOutAt = null,
            DayHours = 0m,
        };

        Hours(input).Should().Be(0m,
            "'có check-in là đủ công' chính là lỗ hổng mà CONG-HOUR-2026-08 đã sửa");
    }

    [Fact(DisplayName = "HỎNG · Bản ghi mất ca ⇒ trần lùi về giờ chuẩn của luật, không tha grace")]
    public void MissingShift_FallsBackToLegalHours()
    {
        var input = From(new TimeOnly(8, 0), new TimeOnly(17, 0)) with { Shift = null, DayHours = 6m };

        Hours(input).Should().Be(6m, "không tra được ca thì không đo được lệch ⇒ tha 0");
    }

    [Fact(DisplayName = "HỎNG · Ca khai StandardHours ≤ 0 ⇒ trần lùi về giờ chuẩn của luật")]
    public void ShiftWithoutStandardHours_FallsBackToLegalHours()
    {
        var input = From(new TimeOnly(8, 0), new TimeOnly(19, 0), shift: OfficeShift(standardHours: 0m));

        Hours(input).Should().Be(8m, "kẹp bằng 8h giờ chuẩn theo luật");
    }
}

/// <summary>
/// CONG-MONTHLY-2026-08 — <see cref="WorkDayCongCalculator.ToCong"/>: cộng GIỜ của cả kỳ rồi mới
/// chia giờ chuẩn/ngày và làm tròn LÊN bội số 0,5.
///
/// <para>Quy tắc đã chốt: phần dư trong <c>(0; 0,5]</c> → 0,5; trong <c>(0,5; 1)</c> → 1.</para>
/// </summary>
public sealed class MonthlyCongRoundingTests
{
    private const decimal HoursPerCong = 8m;

    private static decimal Cong(decimal hours) => WorkDayCongCalculator.ToCong(hours, HoursPerCong);

    [Fact(DisplayName = "CHỐT · Đi làm đủ 22 ngày công chuẩn ⇒ ra ĐÚNG 22 công")]
    public void FullAttendance_MatchesStandardWorkingDays()
    {
        Cong(22 * 8m).Should().Be(22m,
            "đây là tiêu chí nghiệm thu của cả cách tính: người đi làm đủ không được hụt công nào");
    }

    [Theory(DisplayName = "LÀM TRÒN · Làm tròn LÊN bội số 0,5")]
    [InlineData(0, 0)]            // nghỉ trọn kỳ
    [InlineData(4, 0.5)]          // đúng nửa ca ⇒ 0,5 (biên dưới của khoảng (0; 0,5])
    [InlineData(4.1, 1)]          // vượt 0,5 một chút ⇒ lên 1
    [InlineData(8, 1)]            // trọn 1 ca
    [InlineData(154, 19.5)]       // 19,25 ⇒ 19,5
    [InlineData(156, 19.5)]       // 19,50 đã là bội số 0,5 ⇒ giữ nguyên
    [InlineData(157, 20)]         // 19,625 ⇒ 20
    [InlineData(176, 22)]         // 22,00 chằn ⇒ giữ nguyên
    public void CeilsToHalf(decimal hours, decimal expected)
    {
        Cong(hours).Should().Be(expected);
    }

    [Fact(DisplayName = "LÀM TRÒN · Số đã là bội số 0,5 thì giữ nguyên, không đẩy lên tiếp")]
    public void ExactHalfMultiples_AreNotPushedUp()
    {
        Cong(4m).Should().Be(0.5m);
        Cong(12m).Should().Be(1.5m);
        Cong(20m).Should().Be(2.5m);
    }

    [Fact(DisplayName = "LÀM TRÒN · Muộn 1h mỗi ngày cả tháng ⇒ 19,5 công (dồn phần thiếu rồi mới tròn)")]
    public void SmallDailyShortfalls_AccumulateBeforeRounding()
    {
        // 22 ngày × 7h = 154h ⇒ 19,25 công ⇒ làm tròn lên 19,5.
        // Cách cũ (làm tròn từng ngày): 22 × 0,88 = 19,36 — phần thiếu vụn không bao giờ được gộp.
        Cong(22 * 7m).Should().Be(19.5m);
    }

    [Fact(DisplayName = "LÀM TRÒN · Vài phút lẻ trong cả kỳ vẫn ra 0,5 công (đúng quy tắc đã chốt)")]
    public void AnyPositiveWork_EarnsAtLeastHalfACong()
    {
        Cong(0.1m).Should().Be(0.5m, "làm tròn LÊN áp dụng vô điều kiện, luôn nghiêng về phía NLĐ");
    }

    [Fact(DisplayName = "BIÊN · Mẫu số ≤ 0 ⇒ trả 0, không chia cho 0")]
    public void NonPositiveDivisor_ReturnsZero()
    {
        WorkDayCongCalculator.ToCong(100m, 0m).Should().Be(0m);
        WorkDayCongCalculator.ToCong(100m, -8m).Should().Be(0m);
    }
}
