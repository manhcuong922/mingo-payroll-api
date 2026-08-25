using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Common.Services;

/// <summary>Đầu vào tính GIỜ CÔNG ĐƯỢC CÔNG NHẬN của MỘT bản ghi chấm công ca chính.</summary>
/// <param name="Shift">
/// Ca được phân của bản ghi. Null khi bản ghi mất <c>ShiftTemplateId</c> (dữ liệu hỏng) — khi đó
/// trần giờ của ngày lùi về <paramref name="StandardHoursPerDay"/> và không đo được grace.
/// </param>
/// <param name="StandardHoursPerDay">
/// Giờ của MỘT NGÀY CÔNG CHUẨN theo luật (<c>LaborLawConstants.StandardWorkingHoursPerDay</c>).
/// Gánh hai vai: trần giờ khi không tra được ca, và đơn vị quy chuẩn để giờ của các ca dài ngắn
/// khác nhau cộng dồn được với nhau.
/// </param>
public readonly record struct WorkDayInput(
    AttendanceStatus Status,
    DayType DayType,
    DateOnly WorkDate,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    decimal DayHours,
    decimal NightHours,
    bool HasApprovedLateEarly,
    ShiftTemplate? Shift,
    decimal StandardHoursPerDay,
    int LateGraceMinutes,
    int EarlyLeaveGraceMinutes);

/// <summary>
/// ⭐ CONG-MONTHLY-2026-08 — quy đổi chấm công thành CÔNG, theo hai bước tách bạch:
///
/// <list type="number">
///   <item><see cref="RecognizedHours"/> — mỗi ngày ra <b>số giờ công được công nhận</b>, kẹp trần
///     bằng giờ chuẩn của chính ca hôm đó.</item>
///   <item><see cref="ToCong"/> — cộng giờ của <b>cả kỳ</b> rồi mới chia cho giờ chuẩn/ngày và làm
///     tròn LÊN bội số 0,5.</item>
/// </list>
///
/// <para><b>Vì sao dời chỗ làm tròn.</b> Bản trước làm tròn ngay ở từng ngày (2 chữ số thập phân)
/// rồi mới cộng, nên mọi khoản thiếu vụn của từng ngày đều được giữ lại tới cuối kỳ. Nay cộng giờ
/// trước, làm tròn sau: phần thiếu vụn rải rác trong tháng được gộp lại một lần, và làm tròn LÊN
/// nên luôn có lợi cho NLĐ.</para>
///
/// <para><b>Trần theo NGÀY vẫn bắt buộc.</b> Cộng thẳng giờ cả tháng mà không kẹp từng ngày thì ở
/// lại 12h một hôm sẽ đẩy công vượt công chuẩn <b>mà không cần đơn tăng ca nào</b> — vừa trả sai
/// vừa lách được <c>OvertimeCapChecker</c>. Giờ dôi ra ngoài ca phải đi đường đơn OT (Đ.98), không
/// được gói vào công.</para>
///
/// <para>⚠️ Giờ tăng ca KHÔNG bao giờ được vào tổng này — caller phải lọc
/// <c>OvertimeRequestId == null</c>, nếu không OT được trả hai lần (một lần qua công ⇒ cột Thu
/// nhập, một lần qua cột Tăng ca).</para>
///
/// <para><b>Giờ trả về đã QUY CHUẨN về ngày công chuẩn</b>, không phải giờ đồng hồ. Cộng dồn cả kỳ
/// thì các ca dài ngắn khác nhau phải quy về cùng một đơn vị trước, nếu không một ca 4h khai "đáng
/// trọn 1 công" (<c>StandardHours = 4</c>, <c>StandardDays = 1</c>) sẽ chỉ đóng góp 4 giờ và tụt
/// còn nửa công. Công thức: <c>giờ_được_công_nhận ⁄ giờ_ca × giá_trị_công_của_ca × giờ_ngày_chuẩn</c>
/// — với ca 8h/1,00 thông thường thì hệ số bằng 1 và giá trị không đổi.</para>
///
/// <para><b>Grace vẫn có hiệu lực với tiền.</b> <c>AttendanceSetting.LateGraceMinutes</c> vốn chỉ
/// đổi được cái NHÃN (<see cref="AttendanceStatusResolver"/> trả <c>Present</c>) trong khi công
/// chia thẳng giờ thực. Phần lệch nằm TRONG grace được cộng trả vào giờ công được công nhận — chỉ ở
/// tầng này, KHÔNG sửa <c>DayHours</c>/<c>NightHours</c> đã lưu, vì giờ lưu còn là căn cứ của trần
/// OT và bậc hỗ trợ HĐ khoán nên phải giữ đúng giờ làm THẬT.</para>
///
/// <para>Grace là ngưỡng THA, không phải khoản trừ: lệch ≤ grace được tha trọn, lệch &gt; grace
/// không được tha phần nào — cùng ngữ nghĩa với <see cref="AttendanceStatusResolver"/>, nếu không
/// thì nhãn và tiền lại nói hai chuyện khác nhau.</para>
///
/// <para>Ba nhánh KHÔNG đi qua giờ chấm — chúng nhận trọn giờ chuẩn của ca:</para>
/// <list type="bullet">
///   <item><see cref="AttendanceStatus.OnLeave"/> trên ngày nghỉ CÓ LƯƠNG — không có giờ chấm nào
///     để chia, mà `CAC-CASE-XAY-RA.md` §6 quy định đơn nghỉ phép được duyệt thì "sinh ngày nghỉ
///     <b>có lương</b> trong bảng chấm công, <b>chảy vào lương với hệ số ngày phép</b>". Trước đây
///     mọi bản ghi <c>OnLeave</c> đều nhận 0 — kể cả phép năm đã trừ quỹ — nên nghỉ 5 ngày phép là
///     <b>mất luôn 5 ngày lương</b>. Bản thân trạng thái <c>OnLeave</c> không phân biệt được có
///     lương hay không, nên phải tra <see cref="WorkDayInput.DayType"/>: <c>DayTypeResolver</c> chỉ
///     đánh <see cref="DayType.PaidLeaveDay"/> khi có đơn Approved với <c>IsPaid = true</c>, nhờ đó
///     nghỉ KHÔNG lương vẫn giữ 0 giờ.</item>
///   <item><see cref="WorkDayInput.HasApprovedLateEarly"/> — ngày có đơn đi muộn/về sớm đã duyệt
///     hưởng TRỌN ca (quyết định PO 24/08/2026); approver chịu trách nhiệm về phần giờ được miễn,
///     engine không trừ thêm lần nữa.</item>
///   <item>Ca không tra được, hoặc ca khai <c>StandardHours</c> ≤ 0 — lùi về giờ chuẩn/ngày của
///     luật; vẫn không ra thì trả 0 thay vì tự cho đủ ca, vì đó là dữ liệu hỏng chứ không phải bằng
///     chứng đã đi làm.</item>
/// </list>
///
/// <para>⚠️ Bản ghi KHÔNG có giờ (quên chấm ra, hoặc HR nhập tay thiếu giờ) ra 0. CỐ Ý không
/// fallback về trọn ca: chính "có check-in là đủ công" là lỗ hổng mà CONG-HOUR-2026-08 đã sửa.</para>
/// </summary>
public static class WorkDayCongCalculator
{
    /// <summary>
    /// Giờ công được công nhận của một bản ghi, đã kẹp trần bằng giờ chuẩn của ca và quy chuẩn về
    /// ngày công chuẩn để cộng dồn được với các ca khác độ dài.
    /// </summary>
    public static decimal RecognizedHours(in WorkDayInput input)
    {
        var standardDay = input.StandardHoursPerDay;
        var shiftHours = input.Shift is { StandardHours: > 0m } shift ? shift.StandardHours : standardDay;
        if (shiftHours <= 0m) return 0m;

        return Normalize(CreditedHours(input, shiftHours), shiftHours, input.Shift?.StandardDays ?? 1m, standardDay);
    }

    /// <summary>Giờ đồng hồ được công nhận của ngày, trước khi quy chuẩn.</summary>
    private static decimal CreditedHours(in WorkDayInput input, decimal shiftHours)
    {
        if (input.Status == AttendanceStatus.OnLeave)
            return input.DayType == DayType.PaidLeaveDay ? shiftHours : 0m;

        if (input.Status == AttendanceStatus.Absent)
            return 0m;

        if (input.HasApprovedLateEarly)
            return shiftHours;

        var worked = input.DayHours + input.NightHours;
        if (worked <= 0m) return 0m;

        // Làm tròn 2 chữ số như chính cách `WorkHourSplitter` lưu `DayHours`: giờ đã lưu là số đã
        // tròn 2 chữ số, nên cộng thẳng số phút grace vào sẽ hụt vài phần nghìn giờ và ngày được
        // tha trọn vẫn không chạm đúng giờ ca. Vụn đó vô hại về tiền nhưng làm số trên báo cáo
        // trông như bị trừ oan.
        var credited = Math.Round(worked + ForgivenHours(input), 2, MidpointRounding.AwayFromZero);
        return credited > shiftHours ? shiftHours : credited;
    }

    /// <summary>
    /// Quy giờ đồng hồ của ca về giờ của NGÀY CÔNG CHUẨN, giữ nguyên ý nghĩa
    /// <c>ShiftTemplate.StandardDays</c> ("ca này đáng mấy công"). Ca 8h/1,00 ⇒ hệ số 1.
    /// </summary>
    private static decimal Normalize(
        decimal creditedHours, decimal shiftHours, decimal congPerShift, decimal standardDay) =>
        creditedHours / shiftHours * congPerShift * standardDay;

    /// <summary>
    /// Quy đổi TỔNG giờ công của cả kỳ thành số công, làm tròn LÊN bội số 0,5:
    /// phần dư trong <c>(0; 0,5]</c> → 0,5; trong <c>(0,5; 1)</c> → 1.
    ///
    /// <para>0 giờ giữ nguyên 0 công — nghỉ trọn kỳ không thể ra nửa công. Ngoài mốc đó thì làm
    /// tròn lên áp dụng vô điều kiện, kể cả vài phút lẻ ⇒ 0,5 công; đó đúng là quy tắc đã chốt và
    /// luôn nghiêng về phía NLĐ.</para>
    /// </summary>
    public static decimal ToCong(decimal recognizedHours, decimal hoursPerCong)
    {
        if (recognizedHours <= 0m || hoursPerCong <= 0m) return 0m;

        return Math.Ceiling(recognizedHours / hoursPerCong * 2m) / 2m;
    }

    /// <summary>
    /// Số giờ được THA vì phần vào muộn / về sớm nằm trong grace của cấu hình chấm công.
    /// Không tra được ca ⇒ không đo được lệch ⇒ tha 0.
    /// </summary>
    private static decimal ForgivenHours(in WorkDayInput input)
    {
        if (input.Shift is null) return 0m;

        var (lateMinutes, earlyMinutes) = ShiftDeviation.Measure(
            input.CheckInAt, input.CheckOutAt, input.Shift, input.WorkDate);

        var forgiven = Forgive(lateMinutes, input.LateGraceMinutes)
            + Forgive(earlyMinutes, input.EarlyLeaveGraceMinutes);

        return forgiven / 60m;
    }

    private static decimal Forgive(int deviationMinutes, int graceMinutes) =>
        deviationMinutes > 0 && deviationMinutes <= graceMinutes ? deviationMinutes : 0m;
}
